using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a listing is drafted, visible to nobody but its seller.
/// <para>
/// Separate from <see cref="ListingPublishedDomainEvent"/> deliberately: a creation is worth
/// auditing and counting, and emphatically not worth indexing, notifying on or putting in a feed.
/// A draft is not for sale yet, and a buyer whose saved search fired on one would be enquiring
/// about something that does not exist. Every downstream consumer keys on publication.
/// </para>
/// </summary>
public sealed record ListingCreatedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }
}
