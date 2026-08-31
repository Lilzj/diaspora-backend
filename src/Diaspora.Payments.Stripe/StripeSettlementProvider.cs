using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Moves money to a Stripe connected account.
/// <para>
/// <b>A Connect transfer, not a payout.</b> The platform transfers to the connected account; Stripe
/// pays that account out to its own bank on its own schedule. Calling <c>/v1/payouts</c> here would
/// be the platform paying out <em>its own</em> balance to <em>its own</em> bank, which is a
/// different operation that happens to succeed and sends the member nothing.
/// </para>
/// <para>
/// <b>Every transfer carries an idempotency key, and it is the withdrawal's own identifier.</b>
/// Stripe replays the original response for a repeated key rather than moving the money twice, so a
/// submission whose response was lost can be repeated safely. This is the one guarantee that makes
/// the sweep's two-phase shape worth anything: without it, "we do not know whether that went
/// through" has no safe next move.
/// </para>
/// <para>
/// <b>A destination belongs to the provider that issued it.</b> An instruction naming another
/// provider is refused rather than passed through — an account identifier from elsewhere is an
/// opaque string that Stripe would either reject or, worse, match against something of its own.
/// </para>
/// <para>
/// <b>Refused and unknown are different answers and this adapter never conflates them.</b> A 4xx
/// means Stripe declined to create the transfer, so no money moved and the hold may be released. A
/// timeout, a 5xx, a 429 or a 409 means the transfer may exist, and saying "refused" there would
/// have the platform give a member their balance back while Stripe was sending them the same money.
/// <c>SettlementOutcome.Unresolved</c> is the honest answer, and <c>StripeSettlementStatusProvider</c>
/// is how it stops being unresolved.
/// </para>
/// </summary>
public sealed class StripeSettlementProvider : ISettlementProvider
{
    public const string HttpClientName = "payments.stripe";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly HttpClient _http;
    private readonly ILogger<StripeSettlementProvider> _logger;

    public StripeSettlementProvider(
        IOptionsMonitor<StripeOptions> options,
        HttpClient http,
        ILogger<StripeSettlementProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<SettlementOutcome> SubmitAsync(
        SettlementInstruction instruction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instruction);

        var settings = _options.CurrentValue;

        if (!settings.IsConfigured)
        {
            // Unreachable through the registration, which does not register an unconfigured adapter.
            // Kept because "not registered" and "registered without a key" fail very differently and
            // only one of them is obvious from a log line.
            throw new InvalidOperationException(
                $"'{StripeOptions.SectionName}:SecretKey' is not configured.");
        }

        if (!IsOurs(instruction.DestinationProvider, settings.Provider))
        {
            _logger.LogError(
                "Refusing settlement {Reference}: the destination was issued by {Issuer}, not {Provider}.",
                instruction.Reference,
                instruction.DestinationProvider,
                settings.Provider);

            return SettlementOutcome.Refused(
                "stripe.destination_not_ours",
                "The payout destination was issued by another provider.");
        }

        if (string.IsNullOrWhiteSpace(instruction.DestinationReference))
        {
            _logger.LogError(
                "Refusing settlement {Reference}: no connected account was recorded for the "
                + "destination, and Stripe identifies one only by its own account id.",
                instruction.Reference);

            return SettlementOutcome.Refused(
                "stripe.no_connected_account",
                "No Stripe connected account is recorded for that destination.");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("amount", instruction.Amount.MinorUnits.ToString(CultureInfo.InvariantCulture)),

            // Stripe takes lower-case ISO 4217. Money stores it upper-case, and sending it that way
            // is rejected as an unknown currency rather than normalised.
            new("currency", instruction.Amount.Currency.ToLowerInvariant()),
            new("destination", instruction.DestinationReference.Trim())
        };

        if (!string.IsNullOrWhiteSpace(instruction.Reference))
        {
            // Metadata rather than description: it is what reconciliation matches on, and Stripe
            // exposes metadata to search where it does not expose description.
            form.Add(new KeyValuePair<string, string>(
                "metadata[diaspora_withdrawal]", instruction.Reference));

            form.Add(new KeyValuePair<string, string>("transfer_group", instruction.Reference));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{settings.ApiBase.TrimEnd('/')}/v1/transfers")
        {
            Content = new FormUrlEncodedContent(form)
        };

        Authorize(request, settings);

        // The withdrawal's identifier, so a repeat of this exact payout replays rather than repeats.
        // A key derived from the clock or a new Guid would make every retry a fresh transfer, which
        // is the failure this header exists to prevent.
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key", IdempotencyKey(instruction));

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Unknown, not refused: the request may have reached Stripe and been acted on.
            _logger.LogWarning(
                exception,
                "Stripe could not be reached for settlement {Reference}. Whether the transfer was "
                + "created is unknown.",
                instruction.Reference);

            return SettlementOutcome.Unresolved(
                "stripe.unreachable", "Stripe could not be reached; the transfer may exist.");
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var transferId = ReadString(payload, "id");

                _logger.LogInformation(
                    "Stripe accepted settlement {Reference} as transfer {TransferId}.",
                    instruction.Reference,
                    transferId);

                return SettlementOutcome.Accepted(transferId);
            }

            var detail = ReadError(payload);

            if (IsUnknownOutcome(response.StatusCode))
            {
                _logger.LogWarning(
                    "Stripe answered {Status} for settlement {Reference}: {Detail}. The outcome is "
                    + "unknown and the hold stays.",
                    (int)response.StatusCode,
                    instruction.Reference,
                    detail);

                return SettlementOutcome.Unresolved(
                    $"stripe.http_{(int)response.StatusCode}", detail);
            }

            // A 4xx is Stripe declining to create the transfer, which means no money moved. The
            // provider's own wording is the only thing that explains it; the key never appears in it.
            _logger.LogWarning(
                "Stripe refused settlement {Reference} with {Status}: {Detail}",
                instruction.Reference,
                (int)response.StatusCode,
                detail);

            return SettlementOutcome.Refused($"stripe.http_{(int)response.StatusCode}", detail);
        }
    }

    internal static void Authorize(HttpRequestMessage request, StripeOptions settings)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SecretKey);

        if (!string.IsNullOrWhiteSpace(settings.ApiVersion))
        {
            request.Headers.TryAddWithoutValidation("Stripe-Version", settings.ApiVersion);
        }
    }

    /// <summary>
    /// The key a repeat of this payout must reuse. Namespaced, because an idempotency key is
    /// account-wide at Stripe and a bare withdrawal id could collide with another integration's.
    /// </summary>
    internal static string IdempotencyKey(SettlementInstruction instruction) =>
        $"diaspora-payout-{instruction.Reference ?? instruction.CounterpartyId.ToString("N")}";

    private static bool IsOurs(string? issuer, string provider) =>
        // An unbound destination is carried by whoever the router chose, which is this adapter.
        string.IsNullOrWhiteSpace(issuer)
        || string.Equals(issuer.Trim(), provider, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Statuses after which the platform genuinely does not know whether the transfer exists.
    /// <para>
    /// 409 is included on purpose: Stripe returns it when an idempotency key is reused while the
    /// first request is still in flight, which means a transfer may be being created right now.
    /// </para>
    /// </summary>
    private static bool IsUnknownOutcome(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.Conflict
            or HttpStatusCode.TooManyRequests
        || (int)status >= 500;

    private static string ReadError(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("error", out var error))
            {
                return "no error body";
            }

            var code = error.TryGetProperty("code", out var codeElement)
                ? codeElement.GetString()
                : error.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            var message = error.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;

            return string.IsNullOrWhiteSpace(code) ? message ?? "unspecified" : $"{code}: {message}";
        }
        catch (JsonException)
        {
            return "unparseable error body";
        }
    }

    private static string? ReadString(string payload, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);

            return document.RootElement.TryGetProperty(property, out var element)
                ? element.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
