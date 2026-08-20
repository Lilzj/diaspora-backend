using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a listing becomes visible to buyers.
/// <para>
/// <b>The module's most consequential domain fact.</b> It is what puts an item into a search
/// index, fires every matching standing search, starts the seller's response clock and adds a row
/// to the neighbourhood's nearby panel.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> is how a consumer tells "a new item exists" from "an existing
/// item was corrected". A notification fan-out wants the first only — a buyer re-notified because
/// the seller fixed a typo learns to ignore notifications — while a search index wants them all.
/// </para>
/// </summary>
public sealed record ListingPublishedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public UserId? PublishedBy { get; init; }

    /// <summary>False on a republication. Standing searches fire on the first only.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
