using System.Text.Json;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Payments.Stripe;

/// <summary>
/// Turns a verified Stripe charge callback into something the payment path can act on.
/// <para>
/// <b>Separate from the payout processor on purpose, and it must stay separate.</b> They arrive on
/// different routes, and a single processor would have to guess from the body whether an event
/// belongs to a payment or a payout. The route says which, so nothing has to guess — the same
/// reason <c>POST /financial/webhooks/{provider}/payouts</c> is its own endpoint.
/// </para>
/// <para>
/// <b>Verification is shared with the payout processor and is not optional.</b> A correctly signed
/// callback stays correctly signed for ever, so the signature alone proves origin and nothing about
/// replay; the timestamp tolerance is the other half of the check. An unconfigured signing secret
/// means unverifiable, which is reported as unverified rather than waved through — otherwise this
/// endpoint is an unauthenticated way to mark a payment paid.
/// </para>
/// </summary>
public sealed class StripePaymentWebhookProcessor : IPaymentWebhookProcessor
{
    private const string SignatureHeader = "Stripe-Signature";

    private readonly IOptionsMonitor<StripeOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<StripePaymentWebhookProcessor> _logger;

    public StripePaymentWebhookProcessor(
        IOptionsMonitor<StripeOptions> options,
        IClock clock,
        ILogger<StripePaymentWebhookProcessor> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public string Provider => _options.CurrentValue.Provider;

    public Task<PaymentWebhookResult> ProcessAsync(
        PaymentWebhookEnvelope envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var settings = _options.CurrentValue;

        if (!settings.HasWebhookSecret)
        {
            _logger.LogError(
                "A Stripe payment callback arrived and '{Section}:WebhookSecret' is not configured. "
                + "It cannot be verified and was refused.",
                StripeOptions.SectionName);

            return Task.FromResult(PaymentWebhookResult.Unverified());
        }

        var signature = StripePayoutWebhookProcessor.Header(envelope.Headers, SignatureHeader);

        if (signature is null)
        {
            _logger.LogWarning(
                "A Stripe payment callback arrived with no {Header} header.", SignatureHeader);

            return Task.FromResult(PaymentWebhookResult.Unverified());
        }

        if (!StripePayoutWebhookProcessor.Verify(
            signature, envelope.RawBody, settings, _clock.UtcNow, out var reason))
        {
            _logger.LogWarning("A Stripe payment callback failed verification: {Reason}.", reason);

            return Task.FromResult(PaymentWebhookResult.Unverified());
        }

        return Task.FromResult(Translate(envelope.RawBody));
    }

    /// <summary>
    /// Maps a verified Stripe event onto the platform's payment vocabulary.
    /// <para>
    /// <b>An event this adapter does not model is ignored, never guessed at.</b> Stripe sends dozens
    /// of types and a deployment may have subscribed to more than these; treating an unknown type as
    /// a failure would fail payments that had in fact succeeded.
    /// </para>
    /// <para>
    /// Internal so the tests can drive it without constructing a signature, and separate from
    /// verification so neither can accidentally stand in for the other.
    /// </para>
    /// </summary>
    internal static PaymentWebhookResult Translate(string payload)
    {
        JsonElement root;

        try
        {
            root = JsonDocument.Parse(payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return PaymentWebhookResult.Ignored();
        }

        var eventId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

        var kind = type switch
        {
            "payment_intent.succeeded" => "succeeded",
            "payment_intent.payment_failed" => "failed",
            "payment_intent.canceled" => "canceled",

            // The authorisation landed and the amount is now capturable. This is the event that
            // turns a manual-capture intent into money the platform may take, and without it an
            // authorise-then-capture flow has no signal between the two halves.
            "payment_intent.amount_capturable_updated" => "authorized",

            "charge.refunded" => "refunded",
            "charge.dispute.created" => "disputed",

            // The Checkout events, and they are the ones that correlate. A session has no Stripe
            // PaymentIntent id until the member pays, so what the platform stored at authorisation
            // is a cs_... that no payment_intent.* event mentions. These two carry
            // client_reference_id, which is the platform payment id, and the webhook handler falls
            // back to finding an intent by that when the external reference does not resolve.
            //
            // Completed means authorised, not paid: the session was created with
            // capture_method=manual, so the money is held and payment_status stays unpaid until the
            // platform captures.
            "checkout.session.completed" => "authorized",
            "checkout.session.expired" => "canceled",

            _ => null,
        };

        if (kind is null)
        {
            return PaymentWebhookResult.Ignored() with { EventId = eventId };
        }

        if (!root.TryGetProperty("data", out var data)
            || !data.TryGetProperty("object", out var subject))
        {
            return PaymentWebhookResult.Ignored() with { EventId = eventId };
        }

        // Three shapes, in the order that keeps each one matchable.
        //
        // A Checkout session is answered with its client_reference_id, because that is the platform
        // payment id and it is the only handle that exists before the member pays. Its own id would
        // match what was stored, but its payment_intent would not - and preferring payment_intent
        // here would silently stop correlating the moment a session completes.
        //
        // A charge event carries the intent on `payment_intent`; an intent event *is* the intent, so
        // its own `id` is the reference. Reading only one of those would drop every refund and
        // every dispute.
        var reference = ReadClientReference(subject)
            ?? (subject.TryGetProperty("payment_intent", out var intent)
                && intent.ValueKind is JsonValueKind.String
                    ? intent.GetString()
                    : subject.TryGetProperty("id", out var subjectId)
                        ? subjectId.GetString()
                        : null);

        if (string.IsNullOrWhiteSpace(reference))
        {
            return PaymentWebhookResult.Ignored() with { EventId = eventId };
        }

        return new PaymentWebhookResult
        {
            IsVerified = true,
            IsRecognized = true,
            ExternalReference = reference,
            EventKind = kind,
            AmountMinorUnits = ReadAmount(subject, kind),
            Currency = subject.TryGetProperty("currency", out var currency)
                && currency.ValueKind is JsonValueKind.String
                    ? currency.GetString()?.ToUpperInvariant()
                    : null,
            ExternalStatus = subject.TryGetProperty("status", out var status)
                && status.ValueKind is JsonValueKind.String
                    ? status.GetString()
                    : null,
            FailureCode = ReadLastPaymentErrorField(subject, "code"),
            FailureMessage = ReadLastPaymentErrorField(subject, "message"),
            EventId = eventId,
        };
    }

    /// <summary>
    /// The amount this event is about, which is not always the intent's total.
    /// <para>
    /// A refund is for <c>amount_refunded</c>, and reporting the original charge there would tell
    /// the platform a partial refund was a full one. A capturable update is for
    /// <c>amount_capturable</c>, which is what may actually be taken.
    /// </para>
    /// </summary>
    private static long? ReadAmount(JsonElement subject, string kind)
    {
        var property = kind switch
        {
            "refunded" => "amount_refunded",

            // A completed Checkout session reports amount_total and has no amount_capturable; a
            // payment_intent.amount_capturable_updated has the latter and no amount_total. Trying
            // both is what lets one branch serve the two shapes that mean the same thing.
            "authorized" => subject.TryGetProperty("amount_capturable", out _)
                ? "amount_capturable"
                : "amount_total",

            "canceled" => subject.TryGetProperty("amount_total", out _) ? "amount_total" : "amount",
            _ => "amount",
        };

        if (subject.TryGetProperty(property, out var value)
            && value.ValueKind is JsonValueKind.Number
            && value.TryGetInt64(out var amount))
        {
            return amount;
        }

        // Falls back to the plain amount, because a missing specialised field is a shape difference
        // between API versions rather than a reason to report no amount at all.
        return subject.TryGetProperty("amount", out var fallback)
            && fallback.ValueKind is JsonValueKind.Number
            && fallback.TryGetInt64(out var plain)
                ? plain
                : null;
    }

    /// <summary>
    /// The platform payment id a Checkout session was opened for, if this is one.
    /// </summary>
    private static string? ReadClientReference(JsonElement subject) =>
        subject.TryGetProperty("client_reference_id", out var value)
        && value.ValueKind is JsonValueKind.String
        && value.GetString() is { Length: > 0 } reference
            ? reference
            : null;

    private static string? ReadLastPaymentErrorField(JsonElement subject, string property) =>
        subject.TryGetProperty("last_payment_error", out var error)
        && error.ValueKind is JsonValueKind.Object
        && error.TryGetProperty(property, out var value)
        && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;
}
