using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// The one place B7 touches geography.
/// <para>
/// <b>Every geographic decision in this module goes through B4 and comes back here.</b> The
/// client sends city, latitude, longitude and radius in any combination, plus radius sentinels
/// where <c>-1</c> means "this city" and <c>-2</c> means "this country"; all of it is handed to
/// <see cref="IGeographicScopeResolver"/> and normalised once. B7 parses no radius, resolves no
/// city name, computes no haversine and handles no antimeridian — which is precisely what B4's
/// contract freeze promises verticals, and this class is B7 taking it up.
/// </para>
/// <para>
/// The one piece of geometry B7 does own is the <b>bounding box</b>, and it is a prefilter, not
/// an answer: it narrows candidate rows to a rectangle so Location is asked for real distances
/// about tens of providers rather than tens of thousands. Every distance a member ever sees
/// comes from <c>ILocationDirectory</c>.
/// </para>
/// <para>
/// <b>14 Oct 2026: no catalogue.</b> Discovery is answered from the provider's coordinates, its
/// country and the town its owner typed; no area id or path narrows a search, and none is written
/// when a provider is placed. See <see cref="Apply"/>.
/// </para>
/// </summary>
public sealed class GeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;
    private readonly ILocationDirectory _directory;

    public GeographicScopeAdapter(IGeographicScopeResolver resolver, ILocationDirectory directory)
    {
        _resolver = resolver;
        _directory = directory;
    }

    /// <summary>Resolves whatever geography a search request carried.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        ProviderSearchRequest request, UserId? viewerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Without the catalogue (14 Oct 2026). The request's area id, which old app builds still
        // send, is not passed: it must narrow nothing.
        return await _resolver.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                Bounds = request.Bounds,
                UserId = viewerId
            },
            cancellationToken);
    }

    /// <summary>Resolves the geography a provider is being placed at.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        ProviderLocationRequest request, UserId? ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Without the catalogue (14 Oct 2026). A provider is stored at the owner's pin, country
        // and typed town; a town the catalogue does not hold is no longer refused, because nothing
        // finds a provider through the catalogue any more.
        return await _resolver.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                UserId = ownerId
            },
            cancellationToken);
    }

    /// <summary>
    /// The geographic half of a provider filter, from a discovery scope — no catalogue node, no
    /// path (14 Oct 2026).
    /// <list type="bullet">
    /// <item><b>A circle</b>: the rectangle around it over the provider's coordinates (corners
    /// trimmed by <see cref="WithinRadius"/>), plus providers with no coordinates in the town the
    /// request named.</item>
    /// <item><b>A viewport</b>: the rectangle, and no remote providers.</item>
    /// <item><b>A town</b>: the country and the town's name, either letter case.</item>
    /// <item><b>A country</b>: the country.</item>
    /// </list>
    /// </summary>
    public static ProviderQueryFilter Apply(
        ProviderQueryFilter filter, GeographicScope scope, bool aroundPoint)
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
            AreaId = null,
            HierarchyPathPrefix = null,
            AncestorHierarchyPaths = null
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

                // A viewport asks what is inside a rectangle, and a remote provider is not inside
                // anything. Including them — which is right for a radius search, where "I'll do it
                // over video" is a real answer to "who is near me" — would put every remote
                // provider on the platform behind a map the member panned to one street of.
                IncludeRemoteProviders = false
            };
        }

        if (aroundPoint && scope.IsAroundPoint && scope.Bounds is { } circle)
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.CityName,
                MinLatitude = circle.South,
                MaxLatitude = circle.North,
                MinLongitude = circle.West,
                MaxLongitude = circle.East
            };
        }

        // A town or a country. Not the town for a country-wide search: a point's own town is not
        // part of "the whole country".
        return narrowed with
        {
            CityName = scope.Radius.Scope == RadiusScope.Country ? null : scope.CityName,
            MinLatitude = null,
            MaxLatitude = null,
            MinLongitude = null,
            MaxLongitude = null
        };
    }

    /// <summary>
    /// Distances from the scope's centre to each provider, from Location.
    /// <para>
    /// Empty when the scope has no centre, which is the honest answer: a browse with no point to
    /// measure from has no distances, and inventing zeroes would put every provider at the top of
    /// a distance sort.
    /// </para>
    /// </summary>
    /// <summary>
    /// Kilometres from the scope's centre to a position, measured by Location, or null when either
    /// is missing. For a nearest-first search, which holds positions rather than providers.
    /// </summary>
    public double? KilometresFromCentre(GeographicScope scope, double? latitude, double? longitude)
    {
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.Center is not { } origin
            || latitude is not { } lat
            || longitude is not { } lng
            || Location.Domain.ValueObjects.GeoCoordinate.TryCreate(lat, lng) is not { IsSuccess: true } destination)
        {
            return null;
        }

        return _directory.DistanceBetween(origin, destination.Value).Kilometers;
    }

    public IReadOnlyDictionary<Guid, double> MeasureDistances(
        GeographicScope scope, IReadOnlyList<Provider> providers)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(providers);

        if (scope.Center is not { } origin)
        {
            return new Dictionary<Guid, double>();
        }

        var distances = new Dictionary<Guid, double>(providers.Count);

        foreach (var provider in providers)
        {
            if (!provider.Location.HasCoordinates)
            {
                continue;
            }

            var destination = Location.Domain.ValueObjects.GeoCoordinate.Create(
                provider.Location.Latitude!.Value, provider.Location.Longitude!.Value);

            distances[provider.Id.Value] = _directory.DistanceBetween(origin, destination).Kilometers;
        }

        return distances;
    }

    /// <summary>
    /// The providers inside a radius search's circle: the repository answered the rectangle around
    /// it, and this drops the corners. A provider with no position passed the rectangle only
    /// because it serves remotely or names the requested town, and is kept.
    /// </summary>
    public static IReadOnlyList<Provider> WithinRadius(
        GeographicScope scope,
        IReadOnlyList<Provider> providers,
        IReadOnlyDictionary<Guid, double> distances,
        bool aroundPoint)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(distances);

        if (!aroundPoint
            || scope is not { Center: not null, IsViewport: false, Radius.Scope: RadiusScope.Radius })
        {
            return providers;
        }

        var limit = scope.Radius.Distance.Kilometers;

        return
        [
            .. providers.Where(provider =>
                !distances.TryGetValue(provider.Id.Value, out var kilometres) || kilometres <= limit)
        ];
    }
}
