using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Drafts a listing.
/// <para>
/// A draft, never a live listing. Photographs are uploaded over several minutes and prices are
/// reconsidered; publishing straight from creation would put half-written listings with no images
/// in front of buyers, which is the single most common reason a marketplace's search results stop
/// being worth reading.
/// </para>
/// <para>
/// <c>[Transactional]</c>, because the handler writes two aggregates: the listing and its analytics
/// rollup. A listing whose rollup failed to save would be one no view, offer or sale could ever be
/// counted against.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingCreate)]
[Auditable]
[Transactional(Reason = "Writes the listing and its analytics rollup together.")]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Marketplace)]
public sealed record CreateListingCommand : ICommand<ListingResponse>
{
    public required CreateListingRequest Request { get; init; }
}
