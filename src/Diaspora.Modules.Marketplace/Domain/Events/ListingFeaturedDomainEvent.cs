using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when featured placement is given or withdrawn.
/// <para>
/// One event for both directions, with <see cref="IsFeatured"/> saying which. A consumer
/// maintaining a featured rail needs to hear about the withdrawal at least as much as the grant —
/// a rail that only ever gains entries is one that eventually shows nothing but expired
/// promotions.
/// </para>
/// </summary>
public sealed record ListingFeaturedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required bool IsFeatured { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
