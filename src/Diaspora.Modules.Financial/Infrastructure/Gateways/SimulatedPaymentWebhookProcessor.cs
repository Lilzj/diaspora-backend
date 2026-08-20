using System.Globalization;
using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Gateways;

namespace Diaspora.Modules.Financial.Infrastructure.Gateways;

/// <summary>
/// Verifies and translates callbacks from the simulated gateway.
/// <para>
/// <b>Signature verification is real even though the signature is not.</b> The processor computes an
/// HMAC over the raw body with a configured secret and refuses anything that does not match, which
/// means the endpoint's refusal path, its logging and its 400 are all exercised by the suite. A
/// processor that returned <c>IsVerified = true</c> unconditionally would leave the single most
/// security-sensitive branch in this module untested.
/// </para>
/// <para>
/// <b>The raw body is hashed, never a re-serialised object.</b> Verifying a signature over something
/// the sender never sent produces an integration that works until a property order changes.
/// </para>
/// </summary>
public sealed class SimulatedPaymentWebhookProcessor : IPaymentWebhookProcessor
{
    /// <summary>The header the simulator signs with.</summary>
    public const string SignatureHeader = "X-Diaspora-Signature";

    /// <summary>
    /// The development secret. A deployment registering a real processor supplies its own; this one
    /// is a constant precisely because it must never be usable anywhere that matters.
    /// </summary>
    public const string DevelopmentSecret = "simulated-gateway-development-secret";

    public string Provider => SimulatedPaymentGateway.ProviderName;

    public Task<PaymentWebhookResult> ProcessAsync(
        PaymentWebhookEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!IsSignatureValid(envelope))
        {
            return Task.FromResult(PaymentWebhookResult.Unverified());
        }

        try
        {
            using var document = JsonDocument.Parse(envelope.RawBody);
            var root = document.RootElement;

            var eventKind = Text(root, "eventKind");
            var externalReference = Text(root, "externalReference");

            if (eventKind is null || externalReference is null)
            {
                return Task.FromResult(PaymentWebhookResult.Ignored());
            }

            return Task.FromResult(new PaymentWebhookResult
            {
                IsVerified = true,
                IsRecognized = true,
                EventKind = eventKind,
                ExternalReference = externalReference,
                EventId = Text(root, "eventId"),
                ExternalStatus = Text(root, "status"),
                Currency = Text(root, "currency"),
                FailureCode = Text(root, "failureCode"),
                FailureMessage = Text(root, "failureMessage"),
                AmountMinorUnits = Number(root, "amountMinorUnits")
            });
        }
        catch (JsonException)
        {
            // Malformed but correctly signed. Recognised as ours and ignored, rather than retried
            // for ever against a payload that will never parse.
            return Task.FromResult(PaymentWebhookResult.Ignored());
        }
    }

    /// <summary>The signature a caller must send. Public so the suite can produce one.</summary>
    public static string Sign(string rawBody, string secret = DevelopmentSecret)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        var key = System.Text.Encoding.UTF8.GetBytes(secret);
        var payload = System.Text.Encoding.UTF8.GetBytes(rawBody);

        return Convert.ToHexStringLower(
            System.Security.Cryptography.HMACSHA256.HashData(key, payload));
    }

    private static bool IsSignatureValid(PaymentWebhookEnvelope envelope)
    {
        if (!envelope.Headers.TryGetValue(SignatureHeader, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = Sign(envelope.RawBody);

        // Fixed-time comparison: a signature check that returns early on the first differing byte
        // tells an attacker how much of their guess was right.
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(provided.Trim()));
    }

    private static string? Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? Number(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(
                value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                => parsed,
            _ => null
        };
    }
}
