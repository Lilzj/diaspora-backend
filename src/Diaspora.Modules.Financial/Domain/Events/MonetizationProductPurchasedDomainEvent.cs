using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>Somebody bought something from the catalogue and it is paid for.</summary>
public sealed record MonetizationProductPurchasedDomainEvent : DomainEvent
{
    public required MonetizationPurchaseId PurchaseId { get; init; }

    /// <summary>The ledger transaction the capture posted. Null when no money moved through it.</summary>
    public TransactionId? TransactionId { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required string ProductCode { get; init; }

    public required MonetizationProductCategory Category { get; init; }

    public required MonetizationSubjectType SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required long PaidMinorUnits { get; init; }

    public required long ListMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? PromotionCode { get; init; }

    public string? PolicyVersion { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset PurchasedAt { get; init; }
}
