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

    /// <summary>
    /// Resolves a neighbourhood-directory request through the place catalogue.
    /// <para>
    /// The neighbourhood pages are the catalogue's own surface and keep it until Phase 2; a
    /// member's discovery query never comes through here (see <see cref="ResolveAsync"/>).
    /// </para>
    /// </summary>
    public async Task<Result<GeographicScope>> ResolveForNeighborhoodsAsync(
        string? cityName,
        string? countryCode,
        Guid? areaId,
        UserId? viewerId,
        CancellationToken cancellationToken = default) =>
        await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = cityName,
                CountryCode = countryCode,
                AreaId = areaId is { } id && id != Guid.Empty ? AreaId.From(id) : null,
                UserId = viewerId,
                StrictCityResolution = false
            },
            cancellationToken);

    /// <summary>
    /// Resolves whatever geography a search carried, including a map viewport, without the place
    /// catalogue (14 Oct 2026). The area id old app builds still send is accepted and ignored.
    /// </summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        string? cityName,
        string? countryCode,
        Guid? areaId,
        double? latitude,
        double? longitude,
        double? radiusKm,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        _ = areaId;

        return await _resolver.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                CityName = cityName,
                CountryCode = countryCode,
                Latitude = latitude,
                Longitude = longitude,
                RadiusKm = radiusKm,
                Bounds = bounds,
                UserId = viewerId
            },
            cancellationToken);
    }

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
                CityName = null,
                BoundsSouth = Floor(bounds.South),
                BoundsNorth = Ceiling(bounds.North),
                BoundsWest = Floor(bounds.West),
                BoundsEast = Ceiling(bounds.East)
            };

            // No town narrowing on top: a property inside the rectangle but in the town next door
            // is inside the rectangle.
            return narrowed;
        }

        // ── A radius around the member ───────────────────────────────────────────────────────
        //
        // Answered against the coordinate the listing has: the rectangle around the circle,
        // snapped outward like a viewport, and the corners trimmed by the search handler with
        // Location's own distance (see WithinRadius). Since 14 Oct 2026 nothing about the
        // catalogue enters it — the resolver no longer snaps the point to a town — and a listing
        // with no coordinates is kept when it names the town the request named.
        if (aroundPoint && scope.IsAroundPoint && scope.Bounds is { } circle)
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.CityName,
                BoundsSouth = Floor(circle.South),
                BoundsNorth = Ceiling(circle.North),
                BoundsWest = Floor(circle.West),
                BoundsEast = Ceiling(circle.East)
            };
        }

        // A town: the name the landlord typed, either letter case, inside the country. No catalogue
        // path (14 Oct 2026) — a town the catalogue never held, Mombasa or Grand Baie, is found
        // exactly like one it did. Not for a country-wide search, where a point's town is not part
        // of "the whole country".
        if (scope.CityName is { Length: > 0 } city && scope.Radius.Scope != RadiusScope.Country)
        {
            narrowed = narrowed with { CityName = city };
        }

        return narrowed;
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

        // A listing with no coordinates passed the rectangle only by naming the requested town,
        // and there is no distance to trim it by.
        return
        [
            .. listings.Where(listing =>
                listing.Address.Latitude is null
                || (distances.TryGetValue(listing.Id.Value, out var kilometres) && kilometres <= limit))
        ];
    }

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
