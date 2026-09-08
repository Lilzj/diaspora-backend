using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.PayPal;

/// <summary>
/// PayPal's payout callbacks, verified with PayPal.
///
/// <para>
/// <b>For PayPal this is not the fast path — it is the only path.</b>
/// <c>ISettlementStatusProvider</c> resolves a stuck withdrawal by asking the provider about it, and
/// PayPal cannot be asked: the resolution sweep only ever sees withdrawals marked <c>Processing</c>
/// with a <em>null</em> settlement reference — that is the definition of the case it exists for —
/// and PayPal offers no lookup by <c>sender_batch_id</c>, only by the <c>payout_batch_id</c> it
/// returned in the response that was lost. Re-submitting the batch to read it back would create the
/// payout if the original never landed, which is the one thing that must never happen. So a PayPal
/// payout whose response went missing can only be resolved by being told.
/// </para>
///
/// <para>
/// <b>The withdrawal is named by <c>sender_item_id</c>, not by the batch.</b> The settlement adapter
/// already sets it to the instruction's own reference. PayPal's own documentation says the
/// <c>PAYOUTSBATCH</c> events "do not contain item-related information", so the item events are the
/// ones worth listening to — and they are the ones that carry the identifier this platform issued.
/// </para>
///
/// <para>
/// <b>Verification is a round trip, and that is PayPal's design rather than a choice here.</b>
/// Stripe signs with an HMAC that can be checked offline; PayPal signs with a certificate and
/// expects <c>/v1/notifications/verify-webhook-signature</c> to be called with the transmission
/// headers and the raw body. Anything that cannot be verified — no webhook id configured, no
/// token, a non-<c>SUCCESS</c> verification, PayPal unreachable — comes back unverified, and
/// nothing downstream runs. An unverifiable callback saying somebody was paid is precisely the one
/// an attacker would send.
/// </para>
/// </summary>
public sealed class PayPalPayoutWebhookProcessor : IPayoutWebhookProcessor
{
    public const string HttpClientName = "payments.paypal.webhooks";

    private readonly IOptionsMonitor<PayPalOptions> _options;
    private readonly PayPalAccessTokenCache _tokens;
    private readonly HttpClient _http;
    private readonly ILogger<PayPalPayoutWebhookProcessor> _logger;

    public PayPalPayoutWebhookProcessor(
        IOptionsMonitor<PayPalOptions> options,
        PayPalAccessTokenCache tokens,
        HttpClient http,
        ILogger<PayPalPayoutWebhookProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _tokens = tokens;
        _http = http;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public async Task<PayoutWebhookResult> ProcessAsync(
        PayoutWebhookEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var settings = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.WebhookId))
        {
            // Refuses rather than trusts. A deployment that has not configured the webhook id has no
            // way to tell PayPal's callbacks from anybody else's.
            _logger.LogWarning(
                "A PayPal payout callback arrived and no webhook id is configured. Not verified.");

            return Unverified();
        }

        if (!await VerifyAsync(envelope, settings, cancellationToken))
        {
            return Unverified();
        }

        return Read(envelope.RawBody);
    }

    // ── Verification ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Asks PayPal whether it sent this.
    /// <para>
    /// The raw body goes back inside the verification request exactly as it arrived. Re-serialising
    /// it first would be asking PayPal about a document it never sent, which passes in testing and
    /// fails on the first payload whose key order or whitespace differs.
    /// </para>
    /// </summary>
    private async Task<bool> VerifyAsync(
        PayoutWebhookEnvelope envelope, PayPalOptions settings, CancellationToken cancellationToken)
    {
        var token = await _tokens.GetAsync(envelope.ReceivedAt, cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("PayPal would not issue a token to verify a payout callback with.");

            return false;
        }

        // Header names are matched case-insensitively because a proxy may normalise them, and a
        // missing one is a failed verification rather than a thrown request.
        var transmissionId = Header(envelope, "paypal-transmission-id");
        var transmissionTime = Header(envelope, "paypal-transmission-time");
        var transmissionSig = Header(envelope, "paypal-transmission-sig");
        var certUrl = Header(envelope, "paypal-cert-url");
        var authAlgo = Header(envelope, "paypal-auth-algo");

        if (transmissionId is null || transmissionTime is null || transmissionSig is null
            || certUrl is null || authAlgo is null)
        {
            _logger.LogWarning(
                "A PayPal payout callback arrived without its transmission headers. Not verified.");

            return false;
        }

        var body = new StringBuilder()
            .Append('{')
            .Append("\"transmission_id\":").Append(JsonSerializer.Serialize(transmissionId)).Append(',')
            .Append("\"transmission_time\":").Append(JsonSerializer.Serialize(transmissionTime)).Append(',')
            .Append("\"cert_url\":").Append(JsonSerializer.Serialize(certUrl)).Append(',')
            .Append("\"auth_algo\":").Append(JsonSerializer.Serialize(authAlgo)).Append(',')
            .Append("\"transmission_sig\":").Append(JsonSerializer.Serialize(transmissionSig)).Append(',')
            .Append("\"webhook_id\":").Append(JsonSerializer.Serialize(settings.WebhookId)).Append(',')

            // Verbatim, and this is the whole point of keeping the raw body on the envelope.
            .Append("\"webhook_event\":").Append(envelope.RawBody)
            .Append('}')
            .ToString();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.ApiBase.TrimEnd('/')}/v1/notifications/verify-webhook-signature")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;

        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            // Unreachable is unverified. A callback that cannot be checked must not be acted on,
            // and the sweep is what eventually settles the withdrawal instead.
            _logger.LogWarning(exception, "PayPal could not be reached to verify a payout callback.");

            return false;
        }

        using (response)
        {
            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "PayPal refused to verify a payout callback: {Status}.", (int)response.StatusCode);

                return false;
            }

            var status = ReadString(payload, "verification_status");

            if (!string.Equals(status, "SUCCESS", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "A PayPal payout callback did not verify: {Status}.", status ?? "no status");

                return false;
            }

            return true;
        }
    }

    // ── Reading ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What the event says, mapped onto the platform's three words.
    /// <para>
    /// <b>Anything unrecognised is recognised as unrecognised</b>, which leaves the withdrawal held
    /// rather than completing or failing it. PayPal's payout vocabulary is wider than this platform's
    /// and grows; a default that guessed "paid" would pay on an event that meant the opposite.
    /// </para>
    /// </summary>
    private PayoutWebhookResult Read(string rawBody)
    {
        string? eventType;
        JsonElement resource;

        try
        {
            using var document = JsonDocument.Parse(rawBody);
            var root = document.RootElement;

            eventType = root.TryGetProperty("event_type", out var typeElement)
                ? typeElement.GetString()
                : null;

            resource = root.TryGetProperty("resource", out var resourceElement)
                ? resourceElement.Clone()
                : default;
        }
        catch (JsonException)
        {
            _logger.LogWarning("A verified PayPal payout callback could not be parsed.");

            return new PayoutWebhookResult { IsVerified = true, IsRecognized = false };
        }

        var kind = KindOf(eventType);

        if (kind is null)
        {
            // Verified, understood to be PayPal's, and about something this platform does not act
            // on — a batch-level event, or an item state with no ledger meaning.
            return new PayoutWebhookResult { IsVerified = true, IsRecognized = false };
        }

        return new PayoutWebhookResult
        {
            IsVerified = true,
            IsRecognized = true,
            EventKind = kind,

            // The identifier this platform issued, set as sender_item_id when the batch was built.
            // The batch's own sender_batch_id is not on an item event.
            WithdrawalReference = Nested(resource, "payout_item", "sender_item_id"),
            ProviderReference = Property(resource, "payout_item_id")
                ?? Property(resource, "payout_batch_id"),
            FailureCode = kind == "failed" ? eventType : null,
        };
    }

    /// <summary>
    /// PayPal's payout item events, narrowed to the three outcomes a ledger can carry.
    /// <para>
    /// <c>UNCLAIMED</c> and <c>HELD</c> are deliberately absent: the money has left neither PayPal
    /// nor this platform's hold, and an unclaimed payout becomes <c>RETURNED</c> after thirty days
    /// if nobody claims it. Treating it as failed would release a hold while PayPal still has the
    /// money committed; treating it as paid would be worse.
    /// </para>
    /// </summary>
    private static string? KindOf(string? eventType) => eventType switch
    {
        "PAYMENT.PAYOUTS-ITEM.SUCCEEDED" => "paid",

        "PAYMENT.PAYOUTS-ITEM.FAILED" => "failed",
        "PAYMENT.PAYOUTS-ITEM.BLOCKED" => "failed",
        "PAYMENT.PAYOUTS-ITEM.CANCELED" => "failed",
        "PAYMENT.PAYOUTS-ITEM.DENIED" => "failed",

        "PAYMENT.PAYOUTS-ITEM.RETURNED" => "reversed",
        "PAYMENT.PAYOUTS-ITEM.REFUNDED" => "reversed",
        "PAYMENT.PAYOUTS-ITEM.REVERSED" => "reversed",

        _ => null,
    };

    private static PayoutWebhookResult Unverified() =>
        new() { IsVerified = false, IsRecognized = false };

    private static string? Header(PayoutWebhookEnvelope envelope, string name)
    {
        foreach (var pair in envelope.Headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static string? Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;

    private static string? Nested(JsonElement element, string outer, string inner) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(outer, out var parent)
        && parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(inner, out var value)
            ? value.GetString()
            : null;

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
