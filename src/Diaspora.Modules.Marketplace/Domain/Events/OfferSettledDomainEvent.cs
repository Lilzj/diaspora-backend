using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a negotiation ends, however it ends.
/// <para>
/// <b>One event for four outcomes, and the relay splits it.</b> The published catalogue carries
/// <c>OfferAccepted</c> and <c>OfferRejected</c> as separate contracts because those are the two a
/// consumer subscribes to individually; withdrawal and expiry are ordinary and go out on the
/// general contract only. Raising four domain events would put that packaging decision inside the
/// aggregate, where a contract change would have to edit it.
/// </para>
/// </summary>
public sealed record OfferSettledDomainEvent : DomainEvent
{
    public required ListingOfferId OfferId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    /// <summary>How it ended. Never <c>Pending</c> or <c>Countered</c>.</summary>
    public required OfferStatus Status { get; init; }

    /// <summary>Who ended it. Null on an expiry, which the sweep recorded.</summary>
    public UserId? SettledBy { get; init; }

    /// <summary>The amount that stood when it ended. What an accepted sale is priced at.</summary>
    public decimal? Amount { get; init; }

    public required string CurrencyCode { get; init; }

    public required int Quantity { get; init; }

    public string? Reason { get; init; }

    /// <summary>How long the negotiation ran. Feeds the seller's response statistics.</summary>
    public required TimeSpan Duration { get; init; }

    public required DateTimeOffset SettledAt { get; init; }
}
