using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Search;

/// <summary>
/// An arbitrary-shape question: what is inside this outline.
/// <para>
/// The query a drawn search area or a real administrative boundary needs. It is expensive by
/// nature — containment cannot be answered by an index on two columns — so it is executed as
/// a bounding-box filter followed by an exact ring test over the survivors. That is correct
/// on any database and fast enough for the candidate counts a viewport produces; a provider
/// with native geometry support overrides it and pushes the whole thing down.
/// </para>
/// </summary>
public sealed class PolygonSearch
{
    public const int MaxLimit = 500;

    public const int DefaultLimit = 100;

    private readonly AdministrativeLevel[] _levels;

    public GeographicBoundary Boundary { get; }

    /// <summary>The boundary's own extent. The coarse filter, always applied first.</summary>
    public BoundingBox Bounds => Boundary.Extent;

    public IReadOnlyList<AdministrativeLevel> Levels => _levels;

    public CountryCode? CountryCode { get; }

    public int Limit { get; }

    private PolygonSearch(
        GeographicBoundary boundary,
        AdministrativeLevel[] levels,
        CountryCode? countryCode,
        int limit)
    {
        Boundary = boundary;
        _levels = levels;
        CountryCode = countryCode;
        Limit = limit;
    }

    public static Result<PolygonSearch> Create(
        GeographicBoundary boundary,
        IEnumerable<AdministrativeLevel>? levels = null,
        CountryCode? countryCode = null,
        int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        if (boundary.Extent.North - boundary.Extent.South > BoundingBoxSearch.MaxLatitudeSpan)
        {
            return Result.Failure<PolygonSearch>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                $"A search polygon may not span more than {BoundingBoxSearch.MaxLatitudeSpan} " +
                "degrees of latitude."));
        }

        return Result.Success(new PolygonSearch(
            boundary,
            levels?.Distinct().ToArray() ?? [],
            countryCode,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)));
    }

    public bool Accepts(AdministrativeLevel level) =>
        _levels.Length == 0 || _levels.Contains(level);

    public bool Includes(GeoCoordinate candidate) => Boundary.Contains(candidate);
}
