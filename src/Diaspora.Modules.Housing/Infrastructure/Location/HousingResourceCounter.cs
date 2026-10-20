using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Infrastructure.Location;

/// <summary>
/// Tells Location how much housing is in a place.
/// <para>
/// <b>The resource key is <c>housing</c> — one of the keys B4 froze, not a new one.</b> B4's
/// neighbourhood hub has rendered a housing count since it shipped, against the day this module
/// existed; claiming the reserved key means the number stops being zero and Location changes not at
/// all.
/// </para>
/// <para>
/// <b>It is also how Housing avoids referencing its sibling verticals.</b> This module reads the
/// same registry to render jobs, marketplace and provider counts on a neighbourhood page — so the
/// contribution here is one half of a symmetry, not a favour. Jobs does not learn that Housing
/// exists and Housing does not learn that Jobs does.
/// </para>
/// </summary>
public sealed class HousingResourceCounter : ILocationResourceCounter
{
    /// <summary>One of the resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "housing";

    private readonly IHousingListingRepository _listings;

    public HousingResourceCounter(IHousingListingRepository listings) => _listings = listings;

    public string ResourceType => Key;

    public async Task<int> CountInAreaAsync(
        AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        // By path rather than by area identifier, so a city returns everything in its
        // neighbourhoods rather than only the properties filed directly against the city itself.
        return await _listings.CountInAreaAsync(path.Value, cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        CountryCode countryCode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCode);

        return await _listings.CountInCountryAsync(countryCode.Value, cancellationToken);
    }

    /// <summary>
    /// Where the publicly visible homes sit inside a viewport (14 Oct 2026).
    /// <para>
    /// <b>At the coordinate a stranger is shown, never the stored one.</b> A home whose address is
    /// not public is placed at its fuzzed point, exactly as a search result places it, so a small
    /// grid cell cannot say more about where a home is than its card does. The rectangle is
    /// snapped outward to that precision for the same reason. A home with no pin is not on the map.
    /// </para>
    /// </summary>
    public async Task<MapPointPage> ListPointsAsync(
        MapPointQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scale = Math.Pow(10d, PropertyAddress.FuzzedPrecision);
        var limit = Math.Clamp(query.Limit, 1, MapPointReader.MaximumRowsRead);

        var candidates = await _listings.QueryNearestCandidatesAsync(
            new HousingListingQueryFilter
            {
                CountryCode = query.CountryCode,
                BoundsSouth = Math.Floor(query.South * scale) / scale,
                BoundsNorth = Math.Ceiling(query.North * scale) / scale,
                BoundsWest = Math.Floor(query.West * scale) / scale,
                BoundsEast = Math.Ceiling(query.East * scale) / scale,
                Statuses =
                [
                    HousingListingStatus.Available,
                    HousingListingStatus.Published
                ],
                AvailableOnly = true,

                // A public surface: no viewer, so no draft, hidden or unlisted home is in scope.
                ViewerId = null,
                ViewerIsStaff = false
            },
            limit + 1,
            cancellationToken);

        return MapPointReader.FromList(
            query,
            candidates,
            candidate => PropertyAddress.PublicCoordinates(
                candidate.Latitude, candidate.Longitude, candidate.Visibility),
            candidate => candidate.PublishedAt);
    }
}
