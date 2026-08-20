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
