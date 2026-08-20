using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;

namespace Diaspora.Modules.Location.Persistence.Spatial;

/// <summary>
/// The default spatial provider: a bounding-box filter in the database, refined by exact
/// distance in this process.
/// <para>
/// It exists so the platform has working hyperlocal search on PostgreSQL, SQL Server or SQLite
/// with no spatial extension installed, no PostGIS in the container image, and nothing to
/// configure. The technique is standard and the arithmetic is exact: the box is computed to
/// enclose the circle and rounded outward, so nothing in range is ever excluded, and the
/// haversine pass then removes the corners. The only waste is the corner candidates — for a
/// square around a circle, about 21% of the box.
/// </para>
/// <para>
/// <b>What it does not claim.</b> It declares no
/// <see cref="GeospatialCapability.NearestNeighbor"/>, because true nearest-neighbour ordering
/// pushed into the datastore needs a spatial index this provider does not have. It still
/// answers <see cref="FindNearestAsync"/>, by probing an expanding radius — correct and
/// bounded, but not the same capability. A caller that needs the real one can ask the registry
/// and be told no, rather than silently receiving the approximation.
/// </para>
/// <para>
/// A PostGIS provider replaces this by registering under a different key and being named in
/// <c>Location:Spatial:QueryProvider</c>. Nothing above this class changes.
/// </para>
/// </summary>
public sealed class RelationalGeospatialQueryProvider : IGeospatialQueryProvider
{
    /// <summary>The radii tried in turn by <see cref="FindNearestAsync"/>, in kilometres.</summary>
    private static readonly double[] NearestProbeRadii = [2d, 10d, 50d, 250d, 1000d];

    private readonly IAdministrativeAreaRepository _areas;
    private readonly IGeoDistanceCalculator _calculator;

    public RelationalGeospatialQueryProvider(
        IAdministrativeAreaRepository areas, IGeoDistanceCalculator calculator)
    {
        _areas = areas;
        _calculator = calculator;
    }

    public string Key => "relational";

    public GeospatialCapability Capabilities =>
        GeospatialCapability.RadiusSearch |
        GeospatialCapability.BoundingBoxSearch |
        GeospatialCapability.PolygonSearch;

    public async Task<IReadOnlyList<AreaProximityResult>> FindWithinRadiusAsync(
        NearbySearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var candidates = await FetchCandidatesAsync(search, cancellationToken);

        return candidates
            .Where(area => area.Center is not null)
            .Select(area => new AreaProximityResult
            {
                Area = area,
                Distance = _calculator.Between(search.Center, area.Center!)
            })
            .Where(result => search.Radius.Scope != RadiusScope.Radius ||
                             result.Distance! <= search.Radius.Distance)
            .OrderBy(result => result.Distance!.Kilometers)
            .Take(search.Limit)
            .ToList();
    }

    public async Task<IReadOnlyList<AreaProximityResult>> FindWithinBoundsAsync(
        BoundingBoxSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var candidates = await _areas.GetWithinBoundsAsync(
            search.Bounds,
            search.Levels,
            search.CountryCode,
            withinPath: null,
            search.Limit,
            cancellationToken);

        // No distance: a viewport has no centre, and deriving one from the box would produce a
        // number that looks meaningful and is not.
        return candidates
            .Select(area => new AreaProximityResult { Area = area })
            .ToList();
    }

    public async Task<IReadOnlyList<AreaProximityResult>> FindWithinPolygonAsync(
        PolygonSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        // The polygon's own extent narrows the set in the database; the ring test then runs
        // only over what survived. Over-fetching four times the limit leaves room for what the
        // ring rejects — a concave outline can exclude most of its own box.
        var candidates = await _areas.GetWithinBoundsAsync(
            search.Bounds,
            search.Levels,
            search.CountryCode,
            withinPath: null,
            search.Limit * 4,
            cancellationToken);

        return candidates
            .Where(area => area.Center is not null && search.Includes(area.Center))
            .Take(search.Limit)
            .Select(area => new AreaProximityResult { Area = area })
            .ToList();
    }

    public async Task<AreaProximityResult?> FindNearestAsync(
        NearbySearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        // Expanding probe rather than an ordered scan. Ordering the whole table by computed
        // distance would be correct and would also read every row; starting small answers in
        // the first probe for anywhere populated, and only a member in the middle of an ocean
        // pays for the last one.
        foreach (var radiusKm in NearestProbeRadii)
        {
            var radius = Domain.ValueObjects.SearchRadius.FromKilometers(radiusKm);

            if (radius.IsFailure)
            {
                continue;
            }

            var probe = NearbySearch.Create(
                search.Center,
                radius.Value,
                _calculator,
                search.Levels,
                search.CountryCode,
                search.WithinPath,
                limit: 1);

            if (probe.IsFailure)
            {
                continue;
            }

            var found = await FindWithinRadiusAsync(probe.Value, cancellationToken);

            if (found.Count > 0)
            {
                return found[0];
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<AdministrativeArea>> FetchCandidatesAsync(
        NearbySearch search, CancellationToken cancellationToken)
    {
        // A city- or country-wide scope is a hierarchy question, not a spatial one. Answering
        // it by path is both correct and far cheaper than a bounding box the size of a nation.
        if (search.Radius.Scope != RadiusScope.Radius && search.WithinPath is not null)
        {
            return await _areas.GetDescendantsAsync(
                search.WithinPath, search.Levels, search.Limit, cancellationToken);
        }

        // Over-fetch so the corner rejections do not eat into the requested page.
        return await _areas.GetWithinBoundsAsync(
            search.Bounds,
            search.Levels,
            search.CountryCode,
            search.WithinPath,
            search.Limit * 2,
            cancellationToken);
    }
}
