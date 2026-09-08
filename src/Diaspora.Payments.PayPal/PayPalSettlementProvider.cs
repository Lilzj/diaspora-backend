using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.PayPal;

/// <summary>
/// Sends a PayPal payout to one receiver.
/// <para>
/// <b>A one-item batch, because the platform pays one withdrawal at a time.</b> PayPal's payouts API
/// is batch-shaped, and a deployment could group a sweep's worth of instructions into one call — but
/// a batch that partially succeeds gives one HTTP answer for several members' money, and unpicking
/// which of them was paid is a reconciliation problem that a one-item batch never has.
/// </para>
/// <para>
/// <b><c>sender_batch_id</c> is the idempotency key and it is the withdrawal's own identifier.</b>
/// PayPal rejects a batch whose sender id it has already seen, which is what makes a resubmission
/// safe after a lost response.
/// </para>
/// <para>
/// <b>The payload shape here is written from PayPal's REST payouts API and has not been executed
/// against a live account.</b> Until somebody runs <c>LivePayPalSandboxTests</c> with real sandbox
/// credentials, every capability row for this provider should stay <c>Unknown</c> so nothing routes
/// to it. The unit tests pin the request this adapter builds, so a correction is a visible diff
/// rather than a rewrite.
/// </para>
/// </summary>
public sealed class PayPalSettlementProvider : ISettlementProvider
{
    public const string HttpClientName = "payments.paypal";

    private readonly IOptionsMonitor<PayPalOptions> _options;
    private readonly PayPalAccessTokenCache _tokens;
    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger<PayPalSettlementProvider> _logger;

    public PayPalSettlementProvider(
        IOptionsMonitor<PayPalOptions> options,
        PayPalAccessTokenCache tokens,
        HttpClient http,
        IClock clock,
        ILogger<PayPalSettlementProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _tokens = tokens;
        _http = http;
        _clock = clock;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<SettlementOutcome> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var settings = _options.CurrentValue;

        if (!IsOurs(instruction.DestinationProvider, settings.Provider))
        {
            return SettlementOutcome.Refused(
                "paypal.destination_not_ours",
                "The payout destination was issued by another provider.");
        }

        if (string.IsNullOrWhiteSpace(instruction.DestinationReference))
        {
            return SettlementOutcome.Refused(
                "paypal.no_receiver",
                "No PayPal receiver is recorded for that destination.");
        }

        var token = await _tokens.GetAsync(_clock.UtcNow, cancellationToken).ConfigureAwait(false);

        if (token is null)
        {
            // Unknown rather than refused: nothing was sent, but this adapter cannot prove that to
            // the caller any more than a timeout can, and the safe answer holds the money. An
            // operator sees the credential error the token cache logged.
            return SettlementOutcome.Unresolved(
                "paypal.no_access_token", "PayPal would not issue an access token.");
        }

        var batchId = SenderBatchId(instruction);
        var body = BuildPayout(instruction, batchId);

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiBase.TrimEnd('/')}/v1/payments/payouts")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Belt and braces beside sender_batch_id: PayPal honours this header on the payouts route,
        // and the two together mean neither a replayed HTTP request nor a resubmitted batch pays
        // anybody twice.
        request.Headers.TryAddWithoutValidation("PayPal-Request-Id", batchId);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "PayPal could not be reached for settlement {Reference}. Whether the payout was "
                + "created is unknown.",
                instruction.Reference);

            return SettlementOutcome.Unresolved(
                "paypal.unreachable", "PayPal could not be reached; the payout may exist.");
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var batch = ReadBatchId(payload);

                _logger.LogInformation(
                    "PayPal accepted settlement {Reference} as payout batch {BatchId}.",
                    instruction.Reference,
                    batch);

                return SettlementOutcome.Accepted(batch);
            }

            var detail = ReadError(payload);

            // A batch PayPal has already seen is one it already acted on. That is a success being
            // reported as a conflict, and treating it as a refusal would release a hold on money
            // that has gone.
            if (IsDuplicateBatch(response.StatusCode, payload))
            {
                var original = ReadBatchId(payload);

                // The detail is carried into the log because this branch discards it otherwise, and
                // it is the only place the original batch is named. An operator asking "which batch
                // did it already have?" has nowhere else to look.
                // **A warning when it cannot be named, and information when it can.** A
                // withdrawal completing with no provider reference is a reconciliation problem
                // somebody should see: the money moved and the platform holds no handle on it.
                // PayPal returns none on a duplicate, so this is the expected shape of a
                // resubmission after a lost response rather than a fault — but it is still a
                // payout nobody can match, and logging it at Information would bury it.
                if (original is null)
                {
                    _logger.LogWarning(
                        "PayPal reports settlement {Reference} as an already-submitted batch and "
                        + "names no batch id. It is complete and unmatchable. {Detail}",
                        instruction.Reference,
                        detail);
                }
                else
                {
                    _logger.LogInformation(
                        "PayPal reports settlement {Reference} as already-submitted batch {BatchId}. "
                        + "Treating it as accepted.",
                        instruction.Reference,
                        original);
                }

                return SettlementOutcome.Accepted(original);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // The token may simply have aged out mid-flight. Drop it so the next attempt gets a
                // fresh one, and report unknown — a 401 says nothing about whether a payout landed.
                _tokens.Invalidate();

                return SettlementOutcome.Unresolved(
                    "paypal.unauthorized", "PayPal rejected the access token.");
            }

            if (IsUnknownOutcome(response.StatusCode))
            {
                _logger.LogWarning(
                    "PayPal answered {Status} for settlement {Reference}: {Detail}. The outcome is "
                    + "unknown and the hold stays.",
                    (int)response.StatusCode,
                    instruction.Reference,
                    detail);

                return SettlementOutcome.Unresolved(
                    $"paypal.http_{(int)response.StatusCode}", detail);
            }

            _logger.LogWarning(
                "PayPal refused settlement {Reference} with {Status}: {Detail}",
                instruction.Reference,
                (int)response.StatusCode,
                detail);

            return SettlementOutcome.Refused($"paypal.http_{(int)response.StatusCode}", detail);
        }
    }

    /// <summary>
    /// The batch identifier a resubmission must reuse. Namespaced for the same reason Stripe's
    /// idempotency key is: PayPal scopes it to the account, and a bare withdrawal id could collide
    /// with another integration's.
    /// </summary>
    internal static string SenderBatchId(SettlementInstruction instruction) =>
        $"diaspora-payout-{instruction.Reference ?? instruction.CounterpartyId.ToString("N")}";

    internal static string BuildPayout(SettlementInstruction instruction, string batchId)
    {
        var receiver = instruction.DestinationReference!.Trim();

        // An email address and a PayPal payer id are different recipient types and PayPal rejects
        // the wrong one. The '@' is the only thing that distinguishes them without asking.
        var recipientType = receiver.Contains('@', StringComparison.Ordinal) ? "EMAIL" : "PAYPAL_ID";

        // PayPal takes a decimal string, not minor units. The exponent comes off the amount so a
        // zero-decimal currency is not sent with a spurious ".00".
        var value = instruction.Amount.Amount.ToString(
            $"F{instruction.Amount.Exponent}", CultureInfo.InvariantCulture);

        var document = new
        {
            sender_batch_header = new
            {
                sender_batch_id = batchId,
                email_subject = "You have a payout",
                recipient_type = recipientType
            },
            items = new[]
            {
                new
                {
                    recipient_type = recipientType,
                    receiver,
                    sender_item_id = instruction.Reference ?? batchId,
                    amount = new { value, currency = instruction.Amount.Currency }
                }
            }
        };

        return JsonSerializer.Serialize(document);
    }

    private static bool IsOurs(string? issuer, string provider) =>
        string.IsNullOrWhiteSpace(issuer)
        || string.Equals(issuer.Trim(), provider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether PayPal is saying it already has this batch.
    /// <para>
    /// <b>This matched <c>BATCH_ID_ALREADY_EXISTS</c> and PayPal says something else.</b> A live
    /// sandbox run answered <c>USER_BUSINESS_ERROR</c> with the detail "Batch with given
    /// sender_batch_id already exists" on field <c>SENDER_BATCH_ID</c>, so every duplicate was read
    /// as a refusal — and a refusal releases the hold. A resubmission after a lost response is
    /// exactly what the two-phase sweep produces, so the platform would have given the balance back
    /// to somebody PayPal had already paid. The marker was written from documentation and never
    /// executed; the first real call found it.
    /// </para>
    /// <para>
    /// <b>Both directions are dangerous, so this is precise rather than generous.</b> Missing a
    /// duplicate pays twice. Mistaking a genuine refusal for one marks a withdrawal complete that
    /// never happened, debiting a member for a payout nobody sent. It therefore requires the field
    /// PayPal names <em>and</em> the words it uses, not either alone.
    /// </para>
    /// </summary>
    internal static bool IsDuplicateBatch(HttpStatusCode status, string payload)
    {
        if (status is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict))
        {
            return false;
        }

        // Kept: it is what PayPal's documentation describes, and an account or an API version that
        // answers it is still telling us the same thing.
        if (payload.Contains("BATCH_ID_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("details", out var details)
                || details.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var detail in details.EnumerateArray())
            {
                var text = string.Join(
                    ' ',
                    new[] { "issue", "description", "field" }
                        .Select(name => detail.TryGetProperty(name, out var value)
                            ? value.GetString()
                            : null)
                        .Where(value => !string.IsNullOrWhiteSpace(value)));

                if (text.Contains("SENDER_BATCH_ID", StringComparison.OrdinalIgnoreCase)
                    && text.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool IsUnknownOutcome(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    /// <summary>
    /// PayPal's own handle for the batch.
    /// <para>
    /// <b>The link fallback is kept, and a live account does not use it.</b> PayPal's documentation
    /// says a repeated <c>sender_batch_id</c> is rejected with "a HATEOAS link to the original
    /// payout with the same sender_batch_id". A sandbox account on 8 Sep 2026 returned no links at
    /// all — only the name, the message, the detail and a debug id. The branch stays because it is
    /// a pure read that costs nothing and would work on an account or API version that does send
    /// one; it is documented as unobserved so nobody plans around it.
    /// </para>
    /// <para>
    /// <b>So a duplicate is accepted unnamed, and that is PayPal's limitation rather than a gap
    /// here.</b> The case only arises when the first response was lost, which is exactly when
    /// nobody ever learned the batch id. There is no lookup by <c>sender_batch_id</c> to recover
    /// it with.
    /// </para>
    /// </summary>
    internal static string? ReadBatchId(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.TryGetProperty("batch_header", out var header)
                && header.TryGetProperty("payout_batch_id", out var id)
                && id.GetString() is { Length: > 0 } batchId)
            {
                return batchId;
            }

            if (!root.TryGetProperty("links", out var links)
                || links.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var link in links.EnumerateArray())
            {
                if (!link.TryGetProperty("href", out var hrefElement)
                    || hrefElement.GetString() is not { Length: > 0 } href)
                {
                    continue;
                }

                const string Marker = "/payments/payouts/";
                var at = href.IndexOf(Marker, StringComparison.OrdinalIgnoreCase);

                if (at < 0)
                {
                    continue;
                }

                var tail = href[(at + Marker.Length)..].Trim('/');

                // Only the batch's own segment. A link to an item lives under the batch and would
                // name the wrong thing.
                var end = tail.IndexOfAny(['/', '?', '#']);
                var candidate = end < 0 ? tail : tail[..end];

                if (candidate.Length > 0)
                {
                    return candidate;
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// What PayPal actually said, rather than which bucket it filed the complaint under.
    /// <para>
    /// <b>This used to read only <c>name</c> and <c>message</c>, and that made a refusal
    /// undiagnosable.</b> PayPal's top level is a category — <c>USER_BUSINESS_ERROR</c> with the
    /// message "User business error." — and the actual cause lives in <c>details[]</c> as an
    /// <c>issue</c> and a <c>description</c>. The first live sandbox run refused with exactly that
    /// pair and named nothing anybody could act on.
    /// </para>
    /// <para>
    /// <c>debug_id</c> is included because it is the only handle PayPal support will accept when
    /// asked why a payout was refused, and it is gone once the response is discarded.
    /// </para>
    /// </summary>
    internal static string ReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;

            var message = root.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;

            var headline = string.IsNullOrWhiteSpace(name)
                ? message ?? "unspecified"
                : $"{name}: {message}";

            var parts = new List<string> { headline };

            if (root.TryGetProperty("details", out var details)
                && details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    var issue = detail.TryGetProperty("issue", out var issueElement)
                        ? issueElement.GetString()
                        : null;

                    var description = detail.TryGetProperty("description", out var descriptionElement)
                        ? descriptionElement.GetString()
                        : null;

                    var field = detail.TryGetProperty("field", out var fieldElement)
                        ? fieldElement.GetString()
                        : null;

                    var rendered = string.Join(" ", new[] { issue, field, description }
                        .Where(part => !string.IsNullOrWhiteSpace(part)));

                    if (rendered.Length > 0)
                    {
                        parts.Add(rendered);
                    }
                }
            }

            // **The links, because PayPal's documentation has been wrong twice about this body.**
            //
            // It described the duplicate marker as BATCH_ID_ALREADY_EXISTS — a live account says
            // otherwise — and it describes a duplicate as carrying "a HATEOAS link to the original
            // payout", which the first run looking for one did not find. A refusal that names where
            // to look is the difference between a support ticket somebody can act on and one that
            // starts with asking PayPal to resend the body.
            //
            // Hrefs only. A payouts error body can carry a receiver's email address, and a failure
            // message is copied into logs and tickets.
            if (root.TryGetProperty("links", out var links)
                && links.ValueKind == JsonValueKind.Array)
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("href", out var href)
                        && href.GetString() is { Length: > 0 } value)
                    {
                        parts.Add($"link={value}");
                    }
                }
            }

            if (root.TryGetProperty("debug_id", out var debugElement)
                && debugElement.GetString() is { Length: > 0 } debugId)
            {
                parts.Add($"debug_id={debugId}");
            }

            return string.Join(" | ", parts);
        }
        catch (JsonException)
        {
            return "unparseable error body";
        }
    }
}
