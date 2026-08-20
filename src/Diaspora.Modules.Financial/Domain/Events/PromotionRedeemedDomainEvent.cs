using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>A coupon, campaign discount or referral reward was used.</summary>
public sealed record PromotionRedeemedDomainEvent : DomainEvent
{
    public required PromotionRedemptionId RedemptionId { get; init; }

    public required PromotionId PromotionId { get; init; }

    public required string PromotionCode { get; init; }

    public required Guid RedeemedByUserId { get; init; }

    public required long DiscountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? ProductCode { get; init; }

    public required DateTimeOffset RedeemedAt { get; init; }
}
