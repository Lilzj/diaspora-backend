using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// Verifies and translates Flutterwave's transfer callbacks.
/// <para>
/// <b>Flutterwave authenticates a callback with a shared secret echoed in a header, not a
/// signature over the body.</b> That is materially weaker than Stripe's scheme and it is the
/// provider's design rather than a choice available here: the value proves the sender knows the
/// secret and says nothing about the payload, so a body altered in transit would still verify.
/// Two things narrow the gap and both are the platform's own — the comparison below is constant
/// time, and <c>ProcessedWebhookEvent</c> refuses a replayed event id. Neither makes this equivalent
/// to a signed body, and a deployment should terminate these on TLS it controls.
/// </para>
/// <para>
/// <b>One event.</b> <c>transfer.completed</c>, whose <c>data.status</c> says which way it went.
/// Everything else is acknowledged and ignored.
/// </para>
/// </summary>
public sealed class FlutterwavePayoutWebhookProcessor : IPayoutWebhookProcessor
{
    private const string SignatureHeader = "verif-hash";

    private readonly IOptionsMonitor<FlutterwaveOptions> _options;
    private readonly ILogger<FlutterwavePayoutWebhookProcessor> _logger;

    public FlutterwavePayoutWebhookProcessor(
        IOptionsMonitor<FlutterwaveOptions> options,
        ILogger<FlutterwavePayoutWebhookProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public Task<PayoutWebhookResult> ProcessAsync(
        PayoutWebhookEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var settings = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.WebhookSecretHash))
        {
            _logger.LogError(
                "A Flutterwave payout callback arrived and '{Section}:WebhookSecretHash' is not "
                + "configured. It cannot be verified and was refused.",
                FlutterwaveOptions.SectionName);

            return Task.FromResult(PayoutWebhookResult.Unverified());
        }

        var presented = Header(envelope.Headers, SignatureHeader);

        if (presented is null || !Matches(presented, settings.WebhookSecretHash))
        {
            _logger.LogWarning("A Flutterwave payout callback failed verification.");

            return Task.FromResult(PayoutWebhookResult.Unverified());
        }

        return Task.FromResult(Translate(envelope.RawBody));
    }

    /// <summary>
    /// Maps a verified Flutterwave event onto the platform's three payout outcomes.
    /// <para>
    /// <c>NEW</c> and <c>PENDING</c> are deliberately <em>not</em> mapped: a transfer still moving is
    /// not an outcome, and reporting one would drive a withdrawal to a terminal state on a stage
    /// Flutterwave has not finished.
    /// </para>
    /// </summary>
    internal static PayoutWebhookResult Translate(string payload)
    {
        JsonElement root;

        try
        {
            // The document owns a pooled buffer and must be returned; the clone is what
            // outlives it. Parsing and dereferencing in one expression leaked the buffer
            // on every webhook (CA2026, BARCH-003).
            using var document = JsonDocument.Parse(payload);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return PayoutWebhookResult.Ignored();
        }

        var eventName = root.TryGetProperty("event", out var eventElement)
            ? eventElement.GetString()
            : null;

        if (!string.Equals(eventName, "transfer.completed", StringComparison.OrdinalIgnoreCase))
        {
            return PayoutWebhookResult.Ignored();
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return PayoutWebhookResult.Ignored();
        }

        var reference = data.TryGetProperty("reference", out var referenceElement)
            ? referenceElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(reference))
        {
            return PayoutWebhookResult.Ignored();
        }

        var status = data.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()?.ToUpperInvariant()
            : null;

        var kind = status switch
        {
            "SUCCESSFUL" => "paid",
            "FAILED" => "failed",

            // A reversal arrives as its own status on some rails and as a fresh transfer on others.
            // Mapped where it is named and ignored otherwise, rather than inferred.
            "REVERSED" => "reversed",
            _ => null
        };

        if (kind is null)
        {
            return PayoutWebhookResult.Ignored();
        }

        var id = data.TryGetProperty("id", out var idElement)
            ? idElement.ValueKind == JsonValueKind.Number
                ? idElement.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
                : idElement.GetString()
            : null;

        return new PayoutWebhookResult
        {
            IsVerified = true,
            IsRecognized = true,
            WithdrawalReference = Unprefix(reference),
            EventKind = kind,
            ProviderReference = id,
            FailureCode = kind == "paid" ? null : $"flutterwave.{status!.ToLowerInvariant()}",
            FailureMessage = data.TryGetProperty("complete_message", out var message)
                ? message.GetString()
                : null,

            // Flutterwave sends no event identifier of its own, so the handler derives one by
            // hashing the body. That is weaker than a real id — two genuinely distinct callbacks
            // with byte-identical bodies would collapse into one — and strictly better than nothing.
            EventId = null
        };
    }

    /// <summary>
    /// The withdrawal identifier, with the namespace the submission added stripped.
    /// <para>
    /// Flutterwave echoes back the reference the transfer was created with, and
    /// <c>FlutterwaveSettlementProvider.TransferReference</c> namespaces it — so unlike Stripe's
    /// <c>transfer_group</c>, this one really does need unwrapping before it will parse.
    /// </para>
    /// </summary>
    internal static string Unprefix(string reference) =>
        reference.StartsWith("diaspora-payout-", StringComparison.OrdinalIgnoreCase)
            ? reference["diaspora-payout-".Length..]
            : reference;

    /// <summary>
    /// Constant-time comparison. A check that returned early on the first wrong byte would leak how
    /// much of a guessed secret was right, one request at a time.
    /// </summary>
    private static bool Matches(string presented, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));

    private static string? Header(IReadOnlyDictionary<string, string> headers, string name)
    {
        foreach (var pair in headers)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
