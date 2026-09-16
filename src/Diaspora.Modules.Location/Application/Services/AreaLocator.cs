using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Files a vertical's row under the catalogue node it belongs to.
/// <para>
/// <b>The shortest description of what this fixes: before it existed, nothing on the platform was
/// filed anywhere it could be counted.</b> Four verticals stored an area path, four resource
/// counters aggregated by prefix-matching it, the map's cluster endpoint summed those counters — and
/// no create screen ever sent an area, so for homes and jobs the whole chain summed zero. Every part
/// of it was correct.
/// </para>
/// <para>
/// <b>The name decides which place; the point only refines within it.</b> A property and a provider
/// carry a point, and every row carries the town its owner typed. Nearest-centre alone misfiles the
/// rows that matter most: Rose Hill and Quatre Bornes are two and a half kilometres apart, each
/// catalogued as one centre, so a flat on the Quatre Bornes side of the boundary is nearer Rose
/// Hill's centre than its own. The owner said Quatre Bornes; a pin can place the flat in one of
/// Quatre Bornes' neighbourhoods, never in the next town along. A pin is also the weaker witness to
/// which town: it is often the device's position when the form was filled in, or a national centroid
/// for a member whose location is only known to country precision.
/// </para>
/// <para>
/// <b>With no name the catalogue knows, the point decides alone, on a ladder.</b> A catalogue centre
/// stands in for an area with no boundary stored, so "inside it" has to be approximated by distance,
/// and one radius cannot serve every rung: five kilometres for a neighbourhood, twenty-five for a
/// town, sixty for a district. A point in the North Sea matches nothing at any rung, and that is the
/// answer, not a failure.
/// </para>
/// </summary>
public sealed class AreaLocator : IAreaLocator
{
    /// <summary>
    /// How far from a node's centre a row may sit and still be filed under it, per rung.
    /// <para>
    /// Ordered most specific first: the first rung that answers wins, so a flat in Rose Hill
    /// Central is filed there rather than under the district that also contains it.
    /// </para>
    /// </summary>
    private static readonly (AdministrativeLevel Level, double Kilometres)[] PointLadder =
    [
        (AdministrativeLevel.Neighborhood, 5d),
        (AdministrativeLevel.City, 25d),
        (AdministrativeLevel.District, 60d)
    ];

    /// <summary>How many catalogue nodes one lookup may consider.</summary>
    private const int CandidateLimit = 50;

    private const double KilometresPerDegreeLatitude = 111d;

    private readonly IAdministrativeAreaRepository _areas;
    private readonly IGeoDistanceCalculator _distances;

    public AreaLocator(IAdministrativeAreaRepository areas, IGeoDistanceCalculator distances)
    {
        _areas = areas;
        _distances = distances;
    }

    public async Task<AreaPlacement?> LocateAsync(
        AreaFilingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parsed = CountryCode.TryCreate(request.CountryCode);
        var country = parsed.IsSuccess ? parsed.Value : null;

        if (request.AreaId is { } chosen && chosen != Guid.Empty)
        {
            var picked = await _areas.GetByIdAsync(AreaId.From(chosen), cancellationToken);

            // A node in a different country from the row is not a choice anybody made on purpose,
            // and filing under it would count the row on the wrong side of the world.
            if (picked is not null && (country is null || picked.CountryCode == country))
            {
                return Placement(picked);
            }
        }

        // Without a country "London" is three cities on two continents. Filing a row in the wrong
        // one is worse than leaving it unfiled, because a wrong area path is invisible: it makes a
        // cluster count somewhere nobody will check. A point needs no country to mean something.
        var named = country is null
            ? null
            : await FromNamesAsync(request, country, cancellationToken);

        if (request.Latitude is { } latitude
            && request.Longitude is { } longitude
            && GeoCoordinate.TryCreate(latitude, longitude) is { IsSuccess: true } point)
        {
            var refined = await FromPointAsync(point.Value, country, named, cancellationToken);

            if (refined is not null)
            {
                return Placement(refined);
            }
        }

        return named is null ? null : Placement(named);
    }

    /// <summary>
    /// The most specific node the point lands in — strictly inside <paramref name="within"/> when the
    /// row named a place, anywhere in the country when it did not.
    /// </summary>
    private async Task<AdministrativeArea?> FromPointAsync(
        GeoCoordinate point,
        CountryCode? country,
        AdministrativeArea? within,
        CancellationToken cancellationToken)
    {
        foreach (var (level, kilometres) in PointLadder)
        {
            // Inside a named town only a rung below the town can refine it. The town itself is the
            // fallback the caller already holds.
            if (within is not null && level <= within.Level)
            {
                continue;
            }

            var box = BoxAround(point, kilometres);

            if (box is null)
            {
                continue;
            }

            var candidates = await _areas.GetWithinBoundsAsync(
                box,
                [level],
                country,
                withinPath: within?.Path,
                limit: CandidateLimit,
                cancellationToken);

            var nearest = candidates
                .Where(area => area.Center is not null)
                .Select(area => (
                    Area: area,
                    Kilometres: _distances.Between(point, area.Center!).Kilometers))
                .Where(measured => measured.Kilometres <= kilometres)
                .OrderBy(measured => measured.Kilometres)

                // Two centres at the same distance is a tie a database would break by insertion
                // order, which means the same property could file differently on two deployments.
                .ThenBy(measured => measured.Area.Slug.Value, StringComparer.Ordinal)
                .Select(measured => measured.Area)
                .FirstOrDefault();

            if (nearest is not null)
            {
                return nearest;
            }
        }

        return null;
    }

    /// <summary>The most specific place the row names that the catalogue holds, or null.</summary>
    private async Task<AdministrativeArea?> FromNamesAsync(
        AreaFilingRequest request, CountryCode country, CancellationToken cancellationToken)
    {
        foreach (var (name, levels) in NamedPlaces(request))
        {
            var matches = await _areas.FindByNameAsync(
                name, levels, country, CandidateLimit, cancellationToken);

            // The repository ranks by prominence, which is the right tie-break between two real
            // places of the same name. The rung the name was typed for comes first though: a
            // neighbourhood called Stratford beats the city of the same name for a row that named
            // it as its neighbourhood.
            var best = matches
                .OrderBy(area => Array.IndexOf(levels, area.Level))
                .ThenBy(area => area.Slug.Value, StringComparer.Ordinal)
                .FirstOrDefault();

            if (best is not null)
            {
                return best;
            }
        }

        return null;
    }

    /// <summary>
    /// The names a row carries, most specific first, skipping the ones it left blank. Each name
    /// lists the rungs it may match in the order it prefers them.
    /// </summary>
    private static IEnumerable<(string Name, AdministrativeLevel[] Levels)> NamedPlaces(
        AreaFilingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.NeighbourhoodName))
        {
            // A member typing into a neighbourhood box often names the town. Both readings are
            // accepted, and the neighbourhood wins when the catalogue holds both.
            yield return (request.NeighbourhoodName!,
                [AdministrativeLevel.Neighborhood, AdministrativeLevel.City]);
        }

        if (!string.IsNullOrWhiteSpace(request.CityName))
        {
            yield return (request.CityName!,
                [AdministrativeLevel.City, AdministrativeLevel.Neighborhood]);
        }

        if (!string.IsNullOrWhiteSpace(request.RegionName))
        {
            // "Plaines Wilhems" is a district, "Greater London" is a region, and a member typed
            // both into the same field. The catalogue decides which it is.
            yield return (request.RegionName!,
                [AdministrativeLevel.District, AdministrativeLevel.Region,
                    AdministrativeLevel.StateProvince]);
        }
    }

    /// <summary>
    /// A box wide enough to contain every centre within <paramref name="kilometres"/>.
    /// <para>
    /// Longitude degrees shrink toward the poles, so a box built from latitude degrees alone
    /// would be too narrow east-to-west at Manchester and far too narrow at Reykjavik. The cosine
    /// widens it; the floor stops it going infinite at the pole. Over-fetching is free here — the
    /// exact distance filter runs immediately afterwards.
    /// </para>
    /// </summary>
    private static BoundingBox? BoxAround(GeoCoordinate point, double kilometres)
    {
        var latitudeSpan = kilometres / KilometresPerDegreeLatitude;
        var shrink = Math.Max(Math.Cos(point.Latitude * Math.PI / 180d), 0.2d);
        var longitudeSpan = latitudeSpan / shrink;

        var box = BoundingBox.TryCreate(
            north: Math.Min(point.Latitude + latitudeSpan, 90d),
            south: Math.Max(point.Latitude - latitudeSpan, -90d),
            east: Math.Min(point.Longitude + longitudeSpan, 180d),
            west: Math.Max(point.Longitude - longitudeSpan, -180d));

        return box.IsSuccess ? box.Value : null;
    }

    private static AreaPlacement Placement(AdministrativeArea area) =>
        new(area.Id.Value, area.Path.Value, area.Name.Value, area.CountryCode.Value, area.Level);
}
