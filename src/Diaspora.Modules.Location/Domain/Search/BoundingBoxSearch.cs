using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Search;

/// <summary>
/// A viewport question: what is inside this rectangle.
/// <para>
/// The query behind a map. <c>MapPreviewCard</c> currently projects pins client-side from a
/// list it fetched for another reason, which means panning shows nothing new; a viewport
/// query is what makes a map that can be moved.
/// </para>
/// <para>
/// The area cap is the important part. An unbounded viewport query is a full table scan
/// dressed as a spatial one — a client that sends the whole world gets every node in the
/// catalogue, and the request that hurts is the one nobody meant to send.
/// </para>
/// </summary>
public sealed class BoundingBoxSearch
{
    public const int MaxLimit = 500;

    public const int DefaultLimit = 100;

    /// <summary>
    /// The widest span a single viewport query may cover, in degrees of latitude. Roughly
    /// 3,300 km — a continental view, past which the answer belongs to a country list rather
    /// than to a map.
    /// </summary>
    public const double MaxLatitudeSpan = 30d;

    private readonly AdministrativeLevel[] _levels;

    public BoundingBox Bounds { get; }

    public IReadOnlyList<AdministrativeLevel> Levels => _levels;

    public CountryCode? CountryCode { get; }

    public int Limit { get; }

    private BoundingBoxSearch(
        BoundingBox bounds, AdministrativeLevel[] levels, CountryCode? countryCode, int limit)
    {
        Bounds = bounds;
        _levels = levels;
        CountryCode = countryCode;
        Limit = limit;
    }

    public static Result<BoundingBoxSearch> Create(
        BoundingBox bounds,
        IEnumerable<AdministrativeLevel>? levels = null,
        CountryCode? countryCode = null,
        int? limit = null)
    {
        ArgumentNullException.ThrowIfNull(bounds);

        if (bounds.North - bounds.South > MaxLatitudeSpan)
        {
            return Result.Failure<BoundingBoxSearch>(Error.Validation(
                LocationErrorCodes.BoundsInvalid,
                $"A viewport may not span more than {MaxLatitudeSpan} degrees of latitude. " +
                "Zoom in, or browse by country."));
        }

        return Result.Success(new BoundingBoxSearch(
            bounds,
            levels?.Distinct().ToArray() ?? [],
            countryCode,
            Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit)));
    }

    public bool Accepts(AdministrativeLevel level) =>
        _levels.Length == 0 || _levels.Contains(level);
}
