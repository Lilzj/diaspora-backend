using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// The one place B11 turns a client's loose geography into something it can filter with.
/// <para>
/// The client sends a city, an area, coordinates and a radius in any combination, plus B4's radius
/// sentinels; all of it is handed to <see cref="IGeographicScopeResolver"/> and normalised once.
/// B11 parses no radius, resolves no city name and computes no distance — which is precisely what
/// B4's contract freeze promises verticals, and this class is B11 taking it up.
/// </para>
/// <para>
/// <b>Where Housing differs from every other consumer of this seam: a property really is at a
/// point.</b> Marketplace deliberately stores no coordinate because a second-hand item is at
/// somebody's home; a housing listing <em>is</em> somebody's home, and its coordinate is the whole
/// point of the search. The privacy answer is not to withhold the coordinate from the index but to
/// fuzz it on the way out — which <c>PropertyAddress.Disclose</c> does, and which is why proximity
/// search and address privacy can both be honoured at once.
/// </para>
/// <para>
/// <b>An area narrowing prefers the exact neighbourhood over the subtree.</b> A member who picked
/// Peckham means Peckham; a member who picked London means everything under it. B4 resolves which
/// it was and this applies whichever it gave.
/// </para>
/// </summary>
public sealed class HousingGeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;

    public HousingGeographicScopeAdapter(IGeographicScopeResolver resolver) =>
        _resolver = resolver;

    /// <summary>Resolves whatever geography a request carried.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        string? cityName,
        string? countryCode,
        Guid? areaId,
        double? latitude,
        double? longitude,
        double? radiusKm,
        UserId? viewerId,
        CancellationToken cancellationToken = default) =>
        await ResolveAsync(
            cityName, countryCode, areaId, latitude, longitude, radiusKm, bounds: null,
            viewerId, cancellationToken);

    /// <summary>Resolves whatever geography a request carried, including a map viewport.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        string? cityName,
        string? countryCode,
        Guid? areaId,
        double? latitude,
        double? longitude,
        double? radiusKm,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default) =>
        await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = cityName,
                CountryCode = countryCode,
                AreaId = areaId is { } id && id != Guid.Empty ? AreaId.From(id) : null,
                Latitude = latitude,
                Longitude = longitude,
                RadiusKm = radiusKm,
                Bounds = bounds,
                UserId = viewerId,

                // Lenient: a city name the catalogue does not know should narrow nothing rather
                // than fail the browse. Somebody searching for housing in a place they have never
                // been will misspell it, and an error is the worst possible answer to that.
                StrictCityResolution = false
            },
            cancellationToken);

    /// <summary>
    /// Turns a resolved scope into the geographic half of a listing filter.
    /// <para>
    /// An unbounded scope narrows nothing, which is "everywhere" rather than "nowhere", per B4's
    /// own flag.
    /// </para>
    /// </summary>
    public static HousingListingQueryFilter Apply(
        HousingListingQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded)
        {
            return filter;
        }

        var narrowed = filter;

        if (scope.CountryCode is { } country)
        {
            narrowed = narrowed with { CountryCode = country.Value };
        }

        // A viewport is the whole question, and it is answered against the coordinate the listing
        // actually has. Snapped outward to the precision the address is published at, so a
        // rectangle cannot be shrunk around one property until only that property answers —
        // which would be its exact position, obtained by bisection. Widening never loses a row.
        if (scope is { IsViewport: true, Bounds: { } bounds })
        {
            narrowed = narrowed with
            {
                BoundsSouth = Floor(bounds.South),
                BoundsNorth = Ceiling(bounds.North),
                BoundsWest = Floor(bounds.West),
                BoundsEast = Ceiling(bounds.East)
            };

            // No path or city narrowing on top: B4 leaves both null for a viewport precisely so a
            // property inside the rectangle but filed against the city next door is not dropped.
            return narrowed;
        }

        // B4's materialised path, matched as a prefix. The listing stores the same string precisely
        // so this is an indexed LIKE rather than a hierarchy walk.
        //
        // ── DEFECT-010 ───────────────────────────────────────────────────────────────────────
        //
        // A **country root** is excluded, and this is the whole fix. `GeographicScopeResolver`
        // step 5 answers a country-only request with both `CountryCode` and
        // `Path = HierarchyPath.ForRoot(country)`. Narrowing on that path as well is redundant —
        // `CountryCode` is applied above and is an exact match on the same country — but it is
        // not harmless, because the repository matches the path as
        // `AreaPath != null && AreaPath.StartsWith(prefix)`. A listing filed without a
        // neighbourhood has a null `AreaPath`, so the redundant clause deleted it from the
        // result.
        //
        // No create screen sends `neighborhoodAreaId`, so in practice *every* listing has a null
        // path, and `GET /housing/search?countryCode=NG` answered `totalCount: 0` while the same
        // search with no country returned the listings. Marketplace and Jobs never met this
        // because their handlers skip scope resolution when nothing but a country was supplied;
        // Housing resolves unconditionally, which is why it was the only vertical affected.
        //
        // Excluding the root loses nothing: a listing that would match `/NG…` by path is already
        // matched by `CountryCode == "NG"`. Any deeper path still narrows, as it should.
        if (scope.Path is { } path && !IsCountryRoot(path))
        {
            narrowed = narrowed with { AreaPathPrefix = path.Value };
        }
        else if (scope.CityName is { Length: > 0 } city)
        {
            narrowed = narrowed with { CityName = city };
        }

        return narrowed;
    }

    /// <summary>
    /// Whether a path names a country and nothing below it — <c>/NG</c> rather than
    /// <c>/NG/NG-LA/lagos</c>.
    /// <para>
    /// `HierarchyPath.ForRoot` builds exactly one leading separator and one segment, so a root is
    /// the case where no second separator follows.
    /// </para>
    /// </summary>
    private static bool IsCountryRoot(HierarchyPath path) =>
        path.Value.IndexOf(HierarchyPath.Separator, 1) < 0;

    /// <summary>The disclosure grid line at or below a coordinate.</summary>
    private static double Floor(double value)
    {
        var scale = Math.Pow(10d, PropertyAddress.FuzzedPrecision);
        return Math.Floor(value * scale) / scale;
    }

    /// <summary>The disclosure grid line at or above a coordinate.</summary>
    private static double Ceiling(double value)
    {
        var scale = Math.Pow(10d, PropertyAddress.FuzzedPrecision);
        return Math.Ceiling(value * scale) / scale;
    }

    /// <summary>
    /// A human-readable name for whatever scope was resolved, so a results page can say "near
    /// Peckham" rather than echoing what the member typed.
    /// </summary>
    public static string? Describe(GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded)
        {
            return null;
        }

        return scope.CityName is { Length: > 0 } city ? city : scope.CountryCode?.Value;
    }
}
