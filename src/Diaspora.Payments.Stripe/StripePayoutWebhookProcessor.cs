using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Verifies and translates Stripe's payout callbacks.
/// <para>
/// <b>Three events and no more.</b> <c>transfer.paid</c>, <c>transfer.failed</c> and
/// <c>transfer.reversed</c> — a payout left, did not, or came back. Everything else Stripe sends to
/// this endpoint is acknowledged and ignored, because a processor that acted on an event it did not
/// expect is a processor acting on a shape nobody checked.
/// </para>
/// <para>
/// <b>The withdrawal is matched on <c>transfer_group</c>, which is the platform's own reference.</b>
/// Not the transfer id: a submission whose response was lost never returned one, and those are
/// exactly the payouts a callback is most valuable for.
/// </para>
/// <para>
/// <b>Verification is HMAC over <c>{timestamp}.{raw body}</c>, compared in constant time, with the
/// timestamp itself checked against a tolerance.</b> The signature alone does not stop a replay —
/// a captured callback stays validly signed for ever — so the age check is not belt-and-braces, it
/// is the half of the scheme that makes the other half worth anything. The platform's own
/// <c>ProcessedWebhookEvent</c> is the second barrier, and the two are independent.
/// </para>
/// </summary>
public sealed class StripePayoutWebhookProcessor : IPayoutWebhookProcessor
{
    private const string SignatureHeader = "Stripe-Signature";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<StripePayoutWebhookProcessor> _logger;

    public StripePayoutWebhookProcessor(
        IOptionsMonitor<StripeOptions> options,
        IClock clock,
        ILogger<StripePayoutWebhookProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public Task<PayoutWebhookResult> ProcessAsync(
        PayoutWebhookEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var settings = _options.CurrentValue;

        if (!settings.HasWebhookSecret)
        {
            // Unverifiable, so unverified. A deployment that has not configured the signing secret
            // must not have its callbacks believed — treating "we cannot check" as "it is fine"
            // would make this endpoint an unauthenticated way to complete a payout.
            _logger.LogError(
                "A Stripe payout callback arrived and '{Section}:WebhookSecret' is not configured. "
                + "It cannot be verified and was refused.",
                StripeOptions.SectionName);

            return Task.FromResult(PayoutWebhookResult.Unverified());
        }

        var signature = Header(envelope.Headers, SignatureHeader);

        if (signature is null)
        {
            _logger.LogWarning(
                "A Stripe payout callback arrived with no {Header} header.", SignatureHeader);

            return Task.FromResult(PayoutWebhookResult.Unverified());
        }

        if (!Verify(signature, envelope.RawBody, settings, _clock.UtcNow, out var reason))
        {
            _logger.LogWarning("A Stripe payout callback failed verification: {Reason}.", reason);

            return Task.FromResult(PayoutWebhookResult.Unverified());
        }

        return Task.FromResult(Translate(envelope.RawBody));
    }

    /// <summary>
    /// Maps a verified Stripe event onto the platform's three payout outcomes.
    /// <para>
    /// Internal so the tests can drive it without constructing a signature, and separate from
    /// verification so neither can accidentally stand in for the other.
    /// </para>
    /// </summary>
    internal static PayoutWebhookResult Translate(string payload)
    {
        JsonElement root;

        try
        {
            root = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return PayoutWebhookResult.Ignored();
        }

        var eventId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

        var kind = type switch
        {
            "transfer.paid" => "paid",
            "transfer.failed" => "failed",
            "transfer.reversed" => "reversed",
            _ => null
        };

        if (kind is null)
        {
            return PayoutWebhookResult.Ignored(eventId);
        }

        if (!root.TryGetProperty("data", out var data)
            || !data.TryGetProperty("object", out var transfer))
        {
            return PayoutWebhookResult.Ignored(eventId);
        }

        var reference = transfer.TryGetProperty("transfer_group", out var group)
            ? group.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(reference))
        {
            // A transfer with no transfer_group was not created by this platform, or was created
            // before the field was set. Either way there is nothing to match it to.
            return PayoutWebhookResult.Ignored(eventId);
        }

        return new PayoutWebhookResult
        {
            IsVerified = true,
            IsRecognized = true,
            WithdrawalReference = Unprefix(reference),
            EventKind = kind,
            ProviderReference = transfer.TryGetProperty("id", out var transferId)
                ? transferId.GetString()
                : null,
            FailureCode = transfer.TryGetProperty("failure_code", out var failureCode)
                ? failureCode.GetString()
                : null,
            FailureMessage = transfer.TryGetProperty("failure_message", out var failureMessage)
                ? failureMessage.GetString()
                : null,
            EventId = eventId
        };
    }

    /// <summary>
    /// Checks a <c>Stripe-Signature</c> header against the raw body.
    /// <para>
    /// <b>Every candidate <c>v1</c> is tried.</b> Stripe sends more than one while a signing secret
    /// is being rotated, and a verifier that read only the first would reject half the callbacks for
    /// the duration of a rotation — which looks exactly like an attack and is not one.
    /// </para>
    /// </summary>
    internal static bool Verify(
        string header,
        string rawBody,
        StripeOptions settings,
        DateTimeOffset now,
        out string reason)
    {
        long? timestamp = null;
        var candidates = new List<string>();

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator];
            var value = part[(separator + 1)..];

            if (key == "t" && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                timestamp = parsed;
            }
            else if (key == "v1")
            {
                candidates.Add(value);
            }
        }

        if (timestamp is null || candidates.Count == 0)
        {
            reason = "the header carried no timestamp or no v1 signature";
            return false;
        }

        // The age check. A correctly signed callback captured off the wire stays correctly signed
        // for ever, so without this the signature proves origin and nothing about freshness.
        var age = now - DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        var tolerance = TimeSpan.FromSeconds(Math.Max(1, settings.WebhookToleranceSeconds));

        if (age > tolerance || age < -tolerance)
        {
            reason = $"the timestamp is {age.TotalSeconds:F0}s away, outside the tolerance";
            return false;
        }

        var signedPayload =
            $"{timestamp.Value.ToString(CultureInfo.InvariantCulture)}.{rawBody}";

        // Every configured secret is tried, not just one. Stripe issues a secret per endpoint and
        // this integration registers two — payments and payouts on separate routes — so a verifier
        // that knew one would refuse every callback from the other as forged. The same loop covers
        // a secret roll, where Stripe signs with both the old and the new for up to 24 hours.
        //
        // Every candidate is compared even after a match, so the work does not depend on which
        // secret was the right one.
        var verified = false;

        foreach (var secret in settings.AcceptedWebhookSecrets)
        {
            var expected = Sign(signedPayload, secret);

            foreach (var candidate in candidates)
            {
                // Constant time: a comparison that returns early on the first wrong byte leaks how
                // much of a forged signature was right, one request at a time.
                verified |= CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(candidate), Encoding.UTF8.GetBytes(expected));
            }
        }

        if (verified)
        {
            reason = string.Empty;
            return true;
        }

        reason = "no candidate signature matched";
        return false;
    }

    private static string Sign(string payload, string secret) =>
        Convert.ToHexString(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();

    /// <summary>
    /// The withdrawal identifier, however the reference was written.
    /// <para>
    /// The submission sets <c>transfer_group</c> to the bare reference — only the idempotency key is
    /// namespaced — so this is normally a no-op. It is here because a deployment that adopted the
    /// namespaced form, or a transfer created by hand during an incident, would otherwise produce a
    /// reference that parses to nothing and an unmatched callback nobody could explain.
    /// </para>
    /// </summary>
    internal static string Unprefix(string reference) =>
        reference.StartsWith("diaspora-payout-", StringComparison.OrdinalIgnoreCase)
            ? reference["diaspora-payout-".Length..]
            : reference;

    /// <summary>
    /// Case-insensitive header lookup. Internal because the charge processor verifies the
    /// same way against the same header, and two copies would drift.
    /// </summary>
    internal static string? Header(IReadOnlyDictionary<string, string> headers, string name)
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
