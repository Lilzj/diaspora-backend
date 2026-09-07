namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// What a provider's callback turned out to mean, in platform terms.
/// <para>
/// <b>The adapter translates; the platform decides.</b> An adapter says "this reference was captured
/// for this amount"; whether that is legal for the intent in question is the aggregate's decision,
/// and it is made after the adapter has finished. That separation is why a provider cannot drive an
/// intent into a state the state machine forbids.
/// </para>
/// </summary>
public sealed record PaymentWebhookResult
{
    /// <summary>False when the signature did not verify. Nothing downstream runs.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>False when the payload is understood but is about nothing the platform tracks.</summary>
    public required bool IsRecognized { get; init; }

    /// <summary>The provider's reference. How the intent is found.</summary>
    public string? ExternalReference { get; init; }

    /// <summary>
    /// The provider's own handle for this payment, where it differs from the one above.
    /// <para>
    /// <b>Two references, because they do two jobs.</b> <see cref="ExternalReference"/> is what
    /// finds <em>this platform's</em> intent — for a Checkout session that is
    /// <c>client_reference_id</c>, the platform's own id, because it is the only handle that exists
    /// before the member pays. This one is what the platform must quote back <em>to the provider</em>
    /// when it asks for anything.
    /// </para>
    /// <para>
    /// They were one field, and the webhook stored the correlation handle as the gateway reference.
    /// The next call to the provider then quoted the platform's own guid at it and was answered
    /// <c>No such payment_intent</c> — a capture that could never succeed, on money the provider was
    /// holding.
    /// </para>
    /// <para>Null where the provider has no separate id, which is the ordinary case.</para>
    /// </summary>
    public string? ProviderReference { get; init; }

    /// <summary>
    /// What happened. <c>authorized</c>, <c>captured</c>, <c>failed</c>, <c>cancelled</c>,
    /// <c>refunded</c>, <c>settled</c>. A closed vocabulary the adapter maps onto.
    /// </summary>
    public string? EventKind { get; init; }

    public long? AmountMinorUnits { get; init; }

    public string? Currency { get; init; }

    public string? ExternalStatus { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    /// <summary>The provider's own event identifier. The idempotency key for redelivery.</summary>
    public string? EventId { get; init; }

    public static PaymentWebhookResult Unverified() =>
        new() { IsVerified = false, IsRecognized = false };

    public static PaymentWebhookResult Ignored() =>
        new() { IsVerified = true, IsRecognized = false };
}
