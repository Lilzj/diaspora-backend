using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a media reference is removed from a listing.
/// <para>
/// Detaching does not delete the asset, and this module never asks Media to. The member may be
/// using the same photograph on another listing, and a marketplace that deleted the bytes when one
/// reference went would silently blank an unrelated listing.
/// </para>
/// </summary>
public sealed record ListingMediaDetachedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required Guid MediaId { get; init; }

    public required ListingMediaRole Role { get; init; }
}
