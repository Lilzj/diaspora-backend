using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>Money went back.</summary>
public sealed record RefundIssuedDomainEvent : DomainEvent
{
    public required RefundId RefundId { get; init; }

    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>True when the whole captured amount has now been returned.</summary>
    public required bool IsFullRefund { get; init; }

    public required RefundReason Reason { get; init; }

    public MonetizationSubjectType? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>
    /// The member the money was taken back from: the owner of the debited wallet when that wallet is
    /// a person's. Null when it is the platform's (a purchase). See
    /// <c>RefundIssuedIntegrationEvent.PayeeUserId</c>.
    /// </summary>
    public Guid? PayeeUserId { get; init; }
}
