using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Enums;
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
    private readonly ILocationDirectory _directory;

    public HousingGeographicScopeAdapter(
        IGeographicScopeResolver resolver, ILocationDirectory directory)
    {
        _resolver = resolver;
        _directory = directory;
    }

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
    /// <param name="filter">The filter so far.</param>
    /// <param name="scope">What the resolver made of the request's geography.</param>
    /// <param name="aroundPoint">
    /// Whether the request itself carried coordinates. The resolver gives a town search a centre
    /// and a default radius of its own, and a town search must stay a town search.
    /// </param>
    public static HousingListingQueryFilter Apply(
        HousingListingQueryFilter filter, GeographicScope scope, bool aroundPoint = false)
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

        // ── A radius around the member — 15 Sep 2026 ─────────────────────────────────────────
        //
        // "Within 5 km" used to fall through to the path branch below. The resolver snaps a point
        // to the nearest catalogued town and hands back that town's path, so the search answered
        // "listings filed under the nearest town" — and, because no create screen files a listing
        // under an area, nothing at all. A radius is a question about distance, so it is answered
        // against the coordinate the listing has: the rectangle around the circle here, snapped
        // outward like a viewport, and the corners trimmed by the search handler with Location's
        // own distance (see WithinRadius). The town the point happened to be nearest, and any
        // town name the request carried, are not part of the question.
        if (aroundPoint && scope is { Center: not null, Radius.Scope: RadiusScope.Radius, Bounds: { } circle })
        {
            return narrowed with
            {
                CityName = null,
                BoundsSouth = Floor(circle.South),
                BoundsNorth = Ceiling(circle.North),
                BoundsWest = Floor(circle.West),
                BoundsEast = Ceiling(circle.East)
            };
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
            narrowed = narrowed with
            {
                AreaPathPrefix = path.Value,
                AreaPathCityFallback = IsTownPath(path, scope.CityName) ? scope.CityName : null
            };
        }
        else if (scope.CityName is { Length: > 0 } city && scope.Radius.Scope != RadiusScope.Country)
        {
            // Not for a country-wide search. The resolver still names the town nearest the point
            // it was given, and narrowing to it is how "the whole country" came to mean one town.
            narrowed = narrowed with { CityName = city };
        }

        return narrowed;
    }

    /// <summary>
    /// Whether a path ends at the named town itself rather than at a neighbourhood inside it —
    /// <c>/MU/plaines-wilhems/rose-hill</c> for "Rose Hill", and not <c>…/london/peckham</c> for
    /// "London".
    /// </summary>
    private static bool IsTownPath(HierarchyPath path, string? cityName)
    {
        if (string.IsNullOrWhiteSpace(cityName))
        {
            return false;
        }

        var slug = LocationSlug.TryCreate(cityName);

        if (slug.IsFailure)
        {
            return false;
        }

        var last = path.Value[(path.Value.LastIndexOf(HierarchyPath.Separator) + 1)..];

        return string.Equals(last, slug.Value.Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// How far each listing is from the point a search was made around, in kilometres to one
    /// decimal place. Empty when the request carried no point.
    /// <para>
    /// Measured by Location's earth model — Housing computes no distance of its own — and from the
    /// coordinate a signed-out stranger may see, which for most homes is the published, rounded
    /// one. A distance from the exact position would hand back that position to anybody willing to
    /// ask from three places.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<Guid, double> MeasureDistances(
        GeographicScope scope, IReadOnlyList<HousingListing> listings, bool aroundPoint)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(listings);

        if (!aroundPoint || scope.IsViewport || scope.Center is not { } origin)
        {
            return new Dictionary<Guid, double>();
        }

        var distances = new Dictionary<Guid, double>(listings.Count);

        foreach (var listing in listings)
        {
            var visible = listing.Address.Disclose(
                isOwner: false, isStaff: false, isAuthenticated: false, hasConfirmedViewing: false);

            if (visible.Latitude is not { } latitude || visible.Longitude is not { } longitude)
            {
                continue;
            }

            var destination = GeoCoordinate.TryCreate(latitude, longitude);

            if (destination.IsFailure)
            {
                continue;
            }

            distances[listing.Id.Value] = Math.Round(
                _directory.DistanceBetween(origin, destination.Value).Kilometers, 1);
        }

        return distances;
    }

    /// <summary>
    /// Kilometres from a search's point to where a signed-out stranger is shown a home, unrounded,
    /// or null when either end is not a position.
    /// <para>
    /// The measurement "nearest first" orders by across every page, and the same one
    /// <see cref="MeasureDistances"/> puts on each card before rounding it. Here rather than in the
    /// search engine because this class is where Housing reaches Location's earth model, and a
    /// second place measuring would be a second place that could measure from the exact position.
    /// </para>
    /// </summary>
    public double? KilometresToPublishedPosition(
        double originLatitude,
        double originLongitude,
        double? latitude,
        double? longitude,
        AddressVisibility visibility)
    {
        var (publishedLatitude, publishedLongitude) =
            PropertyAddress.PublicCoordinates(latitude, longitude, visibility);

        if (publishedLatitude is not { } lat || publishedLongitude is not { } lng)
        {
            return null;
        }

        var origin = GeoCoordinate.TryCreate(originLatitude, originLongitude);
        var destination = GeoCoordinate.TryCreate(lat, lng);

        if (origin.IsFailure || destination.IsFailure)
        {
            return null;
        }

        return _directory.DistanceBetween(origin.Value, destination.Value).Kilometers;
    }

    /// <summary>
    /// The listings inside a radius search's circle: the repository answered the rectangle around
    /// it, and this drops the corners. Everything else passes through untouched.
    /// </summary>
    public static IReadOnlyList<HousingListing> WithinRadius(
        GeographicScope scope,
        IReadOnlyList<HousingListing> listings,
        IReadOnlyDictionary<Guid, double> distances,
        bool aroundPoint)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(distances);

        if (!aroundPoint
            || scope is not { Center: not null, IsViewport: false, Radius.Scope: RadiusScope.Radius })
        {
            return listings;
        }

        // The distances are rounded to a tenth, so the edge is given the same tenth back.
        var limit = scope.Radius.Distance.Kilometers + 0.05d;

        return
        [
            .. listings.Where(listing =>
                distances.TryGetValue(listing.Id.Value, out var kilometres) && kilometres <= limit)
        ];
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
