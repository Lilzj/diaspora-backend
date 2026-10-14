using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Geography;

namespace Diaspora.Modules.Marketplace.Infrastructure.Location;

/// <summary>
/// Tells the area page how many marketplace listings are pinned within a circle around a point.
/// <para>
/// Active, available and public — the audience the nearby surface contributes, with no viewer — and
/// measured at the pin the seller chose to give. A listing with no pin is not within anything, and
/// a listing that ships is counted where it is, not everywhere it could be posted to.
/// </para>
/// </summary>
public sealed class MarketplaceCircleCounter : ICircleResourceCounter
{
    private readonly IMarketplaceListingRepository _listings;

    public MarketplaceCircleCounter(IMarketplaceListingRepository listings) => _listings = listings;

    public string ResourceType => MarketplaceResourceCounter.Key;

    public async Task<CircleCount> CountWithinAsync(
        CircleArea circle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(circle);

        var filter = new ListingQueryFilter
        {
            MinLatitude = circle.South,
            MaxLatitude = circle.North,
            MinLongitude = circle.West,
            MaxLongitude = circle.East,
            Statuses = [ListingStatus.Active],
            AvailableOnly = true,
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = ListingSortOrder.Newest,
            Limit = MarketplaceLimits.MaxPageSize
        };

        var read = 0;
        var inside = 0;
        string? cursor = null;

        while (read < CircleCount.MaximumRowsRead)
        {
            var page = await _listings.QueryAsync(filter, cursor, filter.EffectiveLimit, cancellationToken);

            read += page.Items.Count;
            inside += page.Items.Count(listing => circle.Contains(listing.Latitude, listing.Longitude));

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        return CircleCount.Of(inside, read);
    }
}
