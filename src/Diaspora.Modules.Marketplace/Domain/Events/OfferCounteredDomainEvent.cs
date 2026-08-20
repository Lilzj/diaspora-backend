using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised on every counter, from either side.
/// <para>
/// <see cref="CounteredBy"/> is what tells a notification consumer who to tell: a seller's counter
/// notifies the buyer, and the buyer's counter-counter notifies the seller. Without it the module
/// would either notify both parties every round or notify the wrong one.
/// </para>
/// </summary>
public sealed record OfferCounteredDomainEvent : DomainEvent
{
    public required ListingOfferId OfferId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    /// <summary>Which side countered.</summary>
    public required OfferParty CounteredBy { get; init; }

    public UserId? ActorId { get; init; }

    public required decimal Amount { get; init; }

    public required string CurrencyCode { get; init; }

    public required int Round { get; init; }

    public required DateTimeOffset CounteredAt { get; init; }
}
