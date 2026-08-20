using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a promotion begins.
/// <para>
/// The handler sets the listing's featured flag when the kind warrants it, so the flag on the
/// listing and the promotion window never disagree — which is what stops a listing staying in the
/// featured rail after the promotion it paid for has ended.
/// </para>
/// </summary>
public sealed record ListingPromotionStartedDomainEvent : DomainEvent
{
    public required ListingPromotionId PromotionId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required PromotionKind Kind { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public UserId? GrantedBy { get; init; }
}
