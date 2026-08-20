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
}
