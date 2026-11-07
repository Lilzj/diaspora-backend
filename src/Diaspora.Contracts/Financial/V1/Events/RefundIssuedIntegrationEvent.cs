using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>Money went back.</summary>
public sealed record RefundIssuedIntegrationEvent : IntegrationEvent
{
    public required Guid RefundId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>True when everything captured has now been returned.</summary>
    public required bool IsFullRefund { get; init; }

    /// <summary>
    /// <c>RequestedByPayer</c>, <c>Duplicate</c>, <c>Fraudulent</c>, <c>NotDelivered</c>,
    /// <c>PolicyRule</c> or <c>Goodwill</c>.
    /// </summary>
    public required string Reason { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// The member the money was taken back from, when it was a member (7 Nov 2026).
    /// <para>
    /// A refund debits whichever wallet the capture credited. For a purchase that is the platform's
    /// revenue and this is null; for a deposit it is the depositor's own wallet, so the payee is the
    /// payer. The payer was told and the payee, who loses the balance, was not. Appended last and
    /// nullable: an event from before this carries none, and a consumer treats that as "no member".
    /// </para>
    /// </summary>
    public Guid? PayeeUserId { get; init; }
}
