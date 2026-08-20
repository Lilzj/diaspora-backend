using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Publishes a listing.
/// <para>
/// Its own command rather than a status field on the update, because publishing is a decision with
/// its own permission and its own consequences — it puts an item in front of strangers with money.
/// A PUT that could do it by accident is one that eventually does.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingPublish)]
[Auditable]
public sealed record PublishListingCommand : ICommand<ListingResponse>
{
    public required Guid ListingId { get; init; }

    public required PublishListingRequest Request { get; init; }
}
