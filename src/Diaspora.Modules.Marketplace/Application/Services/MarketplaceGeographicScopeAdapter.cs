using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// The one place B10 touches geography.
/// <para>
/// The client sends a city, an area, coordinates and a radius in any combination, plus B4's radius
/// sentinels; all of it is handed to <see cref="IGeographicScopeResolver"/> and normalised once.
/// B10 parses no radius, resolves no city name and computes no distance — which is precisely what
/// B4's contract freeze promises verticals, and this class is B10 taking it up.
/// </para>
/// <para>
/// <b>14 Oct 2026: no catalogue.</b> A listing has a country, the town its seller typed and,
/// since the same day, an optional pin the seller chose to give. A circle is answered by the pin
/// (and by unpinned listings in the town the request named), a viewport by the pin, a town by its
/// name in either letter case, a country by the country. No area id or path narrows anything.
/// </para>
/// <para>
/// <b>A shipping listing is deliberately not excluded by a geographic scope.</b> Narrowing to
/// "items near Manchester" and hiding everything a Manchester resident could have posted to them
/// would answer a question nobody asked — the single most common way a marketplace's location
/// filter frustrates the people this platform serves.
/// </para>
/// </summary>
public sealed class MarketplaceGeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;
    private readonly ILocationDirectory _directory;

    public MarketplaceGeographicScopeAdapter(
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

    /// <summary>
    /// Resolves whatever geography a request carried, including a map viewport, without the place
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
    /// <list type="bullet">
    /// <item><b>A viewport</b>: the listings pinned inside it.</item>
    /// <item><b>A circle</b>: the listings pinned inside the rectangle around it (corners trimmed
    /// by <see cref="WithinCircle"/>), unpinned listings in the town the request named, and
    /// listings that ship.</item>
    /// <item><b>A town</b>: the country and the town's name, either letter case.</item>
    /// <item><b>A country</b>: the country.</item>
    /// </list>
    /// An unbounded scope narrows nothing, which is "everywhere" rather than "nowhere".
    /// </summary>
    public static ListingQueryFilter Apply(ListingQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded)
        {
            return filter;
        }

        var narrowed = filter with
        {
            CountryCode = scope.CountryCode?.Value ?? filter.CountryCode,
            AreaPathPrefix = null,
            AreaPathAncestors = null
        };

        if (scope is { IsViewport: true, Bounds: { } viewport })
        {
            return narrowed with
            {
                CityName = null,
                MinLatitude = viewport.South,
                MaxLatitude = viewport.North,
                MinLongitude = viewport.West,
                MaxLongitude = viewport.East,
                IncludeShippingOutsideBox = false
            };
        }

        if (scope.IsAroundPoint && scope.Bounds is { } circle)
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.CityName,
                MinLatitude = circle.South,
                MaxLatitude = circle.North,
                MinLongitude = circle.West,
                MaxLongitude = circle.East,
                IncludeShippingOutsideBox = true
            };
        }

        return narrowed with
        {
            CityName = scope.Radius.Scope == RadiusScope.Country
                ? null
                : scope.CityName ?? filter.CityName
        };
    }

    /// <summary>
    /// The listings inside a circle search's circle: the repository answered the rectangle around
    /// it, and this drops the corners with Location's own distance. A listing with no pin passed
    /// by naming the town or by shipping, and is kept.
    /// </summary>
    public IReadOnlyList<MarketplaceListing> WithinCircle(
        GeographicScope scope, IReadOnlyList<MarketplaceListing> listings)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(listings);

        if (!scope.IsAroundPoint || scope.Center is not { } origin)
        {
            return listings;
        }

        var edge = scope.Radius.Distance.Kilometers;

        return
        [
            .. listings.Where(listing =>
                listing.Fulfillment.OffersShipping
                || listing.Latitude is not { } lat
                || listing.Longitude is not { } lng
                || GeoCoordinate.TryCreate(lat, lng) is not { IsSuccess: true } point
                || _directory.DistanceBetween(origin, point.Value).Kilometers <= edge)
        ];
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

        return scope.CityName is { Length: > 0 } city
            ? city
            : scope.CountryCode?.Value;
    }
}
