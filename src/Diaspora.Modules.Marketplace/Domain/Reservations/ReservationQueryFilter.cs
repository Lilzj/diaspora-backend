using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Reservations;

/// <summary>
/// What a reservation listing can narrow by.
/// <para>
/// As with negotiations, the handler always names a party. A reservation says where somebody is
/// going to be and when, which is the most sensitive thing this module records about anyone.
/// </para>
/// </summary>
public sealed record ReservationQueryFilter
{
    public MarketplaceListingId? ListingId { get; init; }

    public SellerProfileId? SellerId { get; init; }

    public UserId? BuyerUserId { get; init; }

    public IReadOnlyCollection<ReservationStatus>? Statuses { get; init; }

    /// <summary>Only reservations still holding stock. The dashboard's default.</summary>
    public bool ActiveOnly { get; init; }

    public int Limit { get; init; } = MarketplaceLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, MarketplaceLimits.MaxPageSize);
}
