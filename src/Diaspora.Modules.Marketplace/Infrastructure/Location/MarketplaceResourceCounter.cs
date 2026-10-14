using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Infrastructure.Location;

/// <summary>
/// Contributes the listing count to Location's neighbourhood hub and country page.
/// <para>
/// <c>marketplace</c> is one of the resource-count keys B4's contract freeze already published, so
/// B10 claims a key reserved for it rather than inventing one. Together with the nearby provider
/// this is the whole of what B10 adds to Location: two classes, in this module, and nothing at all
/// in that one.
/// </para>
/// <para>
/// Counting is separate from listing because the costs differ by two orders of magnitude, which is
/// why B4 split the seams. Both counts are a single aggregate over an indexed column, and both are
/// live-only: a hub that counted drafts would advertise goods nobody can buy.
/// </para>
/// </summary>
public sealed class MarketplaceResourceCounter : ILocationResourceCounter
{
    /// <summary>The resource-count key B4's contract freeze reserved for the marketplace.</summary>
    public const string Key = "marketplace";

    private readonly IMarketplaceListingRepository _listings;

    public MarketplaceResourceCounter(IMarketplaceListingRepository listings) =>
        _listings = listings;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Prefix match on the materialised path, so "London" counts everything beneath it without a
        // recursive descent. A listing scoped only to its country contributes to no neighbourhood,
        // which is correct: a thing for sale somewhere in the UK is not a thing for sale in Peckham.
        return await _listings.CountInAreaAsync(path.Value, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _listings.CountInCountryAsync(countryCode.Value, cancellationToken);
    }

    /// <summary>
    /// Where the live public listings sit inside a viewport, by the pin each listing carries
    /// (14 Oct 2026). A listing with no pin is not on the map, and shipping does not widen the box.
    /// </summary>
    public Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new ListingQueryFilter
        {
            CountryCode = query.CountryCode,
            MinLatitude = query.South,
            MaxLatitude = query.North,
            MinLongitude = query.West,
            MaxLongitude = query.East,
            IncludeShippingOutsideBox = false,
            Statuses = [ListingStatus.Active],
            AvailableOnly = true,

            // A public surface: no viewer, so no draft, unlisted or hidden listing is in scope.
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = ListingSortOrder.Newest
        };

        return MapPointReader.ReadAsync(
            query,
            async (cursor, size, token) =>
            {
                var page = await _listings.QueryAsync(filter, cursor, size, token);
                return (page.Items, page.HasMore ? page.NextCursor : null);
            },
            listing => (listing.Latitude, listing.Longitude),
            MarketplaceLimits.MaxPageSize,
            cancellationToken);
    }
}
