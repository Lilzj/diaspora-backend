using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when the seller changes something a buyer reads.
/// <para>
/// <b><see cref="ChangedFields"/> is what makes this event usable rather than merely correct.</b>
/// A price change matters to everyone watching the listing; a tag change matters to the search
/// index and to nobody else. A consumer that could only see "something changed" would either
/// notify on everything or on nothing.
/// </para>
/// </summary>
public sealed record ListingUpdatedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public UserId? ChangedBy { get; init; }

    /// <summary>Short field names: <c>title</c>, <c>price</c>, <c>stock</c>, <c>location</c>.</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}
