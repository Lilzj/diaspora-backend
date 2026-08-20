using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Search;

/// <summary>
/// A proximity question: what is near this point, how near counts, and what kinds of thing
/// are being asked about.
/// <para>
/// A validated value object rather than a bag of parameters, because the same question is
/// asked by five endpoints and by every future vertical through
/// <c>INearbyResourceProvider</c>. Validating once at construction means no provider has to
/// re-check that the radius is positive or the centre is on the planet, and no provider can
/// forget to.
/// </para>
/// <para>
/// <see cref="Bounds"/> is derived at construction rather than by each provider. It is the
/// coarse filter every implementation needs, and computing it here means the relational
/// provider and a future PostGIS provider are filtering to exactly the same rectangle.
/// </para>
/// </summary>
public sealed class NearbySearch
{
    /// <summary>The cap on how many results any one provider may return.</summary>
    public const int MaxLimit = 200;

    public const int DefaultLimit = 50;

    private readonly AdministrativeLevel[] _levels;

    public GeoCoordinate Center { get; }

    public SearchRadius Radius { get; }

    /// <summary>
    /// The box guaranteed to contain every point in range. Meaningless — and ignored — when
    /// <see cref="SearchRadius.Scope"/> is not <see cref="RadiusScope.Radius"/>.
    /// </summary>
    public BoundingBox Bounds { get; }

    /// <summary>Restricts results to these levels. Empty means every level.</summary>
    public IReadOnlyList<AdministrativeLevel> Levels => _levels;

    /// <summary>Restricts results to one country. Set by a country-scoped search.</summary>
    public CountryCode? CountryCode { get; }

    /// <summary>
    /// Restricts results to descendants of this node. How
    /// <see cref="RadiusScope.City"/> and <see cref="RadiusScope.Country"/> are actually
    /// executed: the scope resolves to an ancestor and the query becomes a path prefix match.
    /// </summary>
    public HierarchyPath? WithinPath { get; }

    public int Limit { get; }

    private NearbySearch(
        GeoCoordinate center,
        SearchRadius radius,
        BoundingBox bounds,
        AdministrativeLevel[] levels,
        CountryCode? countryCode,
        HierarchyPath? withinPath,
        int limit)
    {
        Center = center;
        Radius = radius;
        Bounds = bounds;
        _levels = levels;
        CountryCode = countryCode;
        WithinPath = withinPath;
        Limit = limit;
    }

    public static Result<NearbySearch> Create(
        GeoCoordinate center,
        SearchRadius radius,
        Spatial.IGeoDistanceCalculator calculator,
        IEnumerable<AdministrativeLevel>? levels = null,
        CountryCode? countryCode = null,
        HierarchyPath? withinPath = null,
        int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(radius);
        ArgumentNullException.ThrowIfNull(calculator);

        // A non-radius scope still needs a box for providers that only know how to filter
        // spatially; the whole world is the honest one, and the path predicate does the real
        // narrowing.
        var bounds = radius.Scope == RadiusScope.Radius
            ? calculator.BoundingBoxAround(center, radius.Distance)
            : BoundingBox.World;

        return Result.Success(new NearbySearch(
            center,
            radius,
            bounds,
            levels?.Distinct().ToArray() ?? [],
            countryCode,
            withinPath,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)));
    }

    /// <summary>Whether a point is in range under this search's scope.</summary>
    public bool Includes(GeoCoordinate candidate, Spatial.IGeoDistanceCalculator calculator)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(calculator);

        // City- and country-wide searches are answered by the path predicate, so every point
        // that survived it is in range by definition.
        if (Radius.Scope != RadiusScope.Radius)
        {
            return true;
        }

        return Bounds.Contains(candidate) &&
               calculator.Between(Center, candidate) <= Radius.Distance;
    }

    /// <summary>Whether a level passes this search's level filter.</summary>
    public bool Accepts(AdministrativeLevel level) =>
        _levels.Length == 0 || _levels.Contains(level);
}
