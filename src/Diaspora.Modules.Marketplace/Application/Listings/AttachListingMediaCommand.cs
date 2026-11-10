using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Attaches a photograph, video or document to a listing, by reference.
/// <para>
/// The bytes went to Media first; this carries only the identifier it returned. The handler for the
/// event this raises is what claims the asset, and claiming is what stops Media sweeping it a day
/// later.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record AttachListingMediaCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required AttachListingMediaRequest Request { get; init; }
}
