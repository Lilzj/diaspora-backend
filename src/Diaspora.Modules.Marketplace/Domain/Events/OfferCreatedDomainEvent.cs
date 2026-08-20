using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a buyer opens a negotiation.
/// <para>
/// Carries the amount, because the one consumer that matters — the seller's notification — is
/// useless without it. It deliberately carries no buyer profile content: an offer is personal data
/// and the bus is not the place for it.
/// </para>
/// </summary>
public sealed record OfferCreatedDomainEvent : DomainEvent
{
    public required ListingOfferId OfferId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    public required decimal Amount { get; init; }

    public required string CurrencyCode { get; init; }

    public required int Quantity { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
