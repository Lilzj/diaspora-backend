using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Removes a media reference from a listing.
/// <para>
/// <b>Detaching never deletes the asset, and this module never asks Media to.</b> The member may be
/// using the same photograph on another listing, and a marketplace that deleted the bytes when one
/// reference went would silently blank an unrelated one.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record RemoveListingMediaCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary>Which role to detach. Every role the asset holds when omitted.</summary>
    public string? Role { get; init; }
}
