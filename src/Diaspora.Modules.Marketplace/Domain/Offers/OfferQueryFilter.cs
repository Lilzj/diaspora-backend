using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Offers;

/// <summary>
/// What a negotiation listing can narrow by.
/// <para>
/// <b>Either <see cref="BuyerUserId"/> or <see cref="SellerId"/> is always set by the handler.</b>
/// A negotiation is private to its two parties, and a filter that could be run without naming one
/// of them would be a way to read everybody's offers. The repository does not enforce that — the
/// handler does, because only the handler knows who is asking.
/// </para>
/// </summary>
public sealed record OfferQueryFilter
{
    public MarketplaceListingId? ListingId { get; init; }

    public SellerProfileId? SellerId { get; init; }

    public UserId? BuyerUserId { get; init; }

    public IReadOnlyCollection<OfferStatus>? Statuses { get; init; }

    /// <summary>Only negotiations still awaiting somebody. The dashboard's default.</summary>
    public bool OpenOnly { get; init; }

    /// <summary>Only ones waiting on this party. What "your turn" is.</summary>
    public OfferParty? AwaitingParty { get; init; }

    public int Limit { get; init; } = MarketplaceLimits.DefaultPageSize;

    public int EffectiveLimit => Math.Clamp(Limit, 1, MarketplaceLimits.MaxPageSize);
}
