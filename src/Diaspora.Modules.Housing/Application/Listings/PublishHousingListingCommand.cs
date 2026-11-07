using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Puts a property on the market.
/// <para>
/// The one write path that consults the fraud seam. Where the deployment enforces signals, a
/// blocking signal holds the listing rather than publishing it.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingPublish)]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Housing)]
public sealed record PublishHousingListingCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public PublishHousingListingRequest? Request { get; init; }
}
