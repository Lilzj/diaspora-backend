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
/// The ancestor-path list is the other thing that happens here, and it exists because
/// containment runs in two directions: "provider inside London" is a prefix match on the
/// provider's own path, while "service area covers Peckham" needs every ancestor of the request
/// enumerated so the query can be an indexed <c>IN</c>. See
/// <c>ProviderQueryFilter.AncestorHierarchyPaths</c>.
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

        return await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                AreaId = request.AreaId is { } areaId ? AreaId.From(areaId) : null,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                Bounds = request.Bounds,
                UserId = viewerId,

                // Lenient: a city name the catalogue does not know should narrow nothing rather
                // than fail the browse. A discovery screen that errors because somebody typed
                // "Londn" is worse than one that shows everything.
                StrictCityResolution = false
            },
            cancellationToken);
    }

    /// <summary>Resolves the geography a provider is being placed at.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        ProviderLocationRequest request, UserId? ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                AreaId = request.AreaId is { } areaId ? AreaId.From(areaId) : null,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                UserId = ownerId,

                // Strict here, and lenient above, on purpose. A search that silently ignores a
                // bad city shows too much; a provider stored against a city that does not exist
                // is invisible forever and nobody finds out why.
                StrictCityResolution = true
            },
            cancellationToken);
    }

    /// <summary>
    /// Turns a resolved scope into the geographic half of a query filter.
    /// <para>
    /// The bounding box is taken from B4's own <c>Bounds</c> when it computed one. B7 never
    /// derives a rectangle from a radius itself — that calculation has an antimeridian case and a
    /// latitude-convergence case, and B4 has both and a test suite for them.
    /// </para>
    /// </summary>
    public async Task<ProviderQueryFilter> ApplyAsync(
        ProviderQueryFilter filter, GeographicScope scope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded)
        {
            return filter;
        }

        // B4 supplies the path whenever the scope resolved through the catalogue, but a scope
        // built from an explicit area id the client already held arrives with the id and no path.
        // Fetching it here rather than leaving it null is the difference between a subtree
        // filter and a single-node one — and a member searching "London" who only sees providers
        // filed against London itself, and none in Peckham, would have no way to tell why.
        var path = scope.Path?.Value;

        if (path is null && scope.AreaId is { } areaId)
        {
            var area = await _directory.FindAreaAsync(areaId, cancellationToken);
            path = area?.Path.Value;
        }

        var ancestors = path is null ? null : AncestorPaths(path);

        return filter with
        {
            CountryCode = scope.CountryCode?.Value,
            AreaId = scope.AreaId?.Value,
            HierarchyPathPrefix = path,
            AncestorHierarchyPaths = ancestors,
            CityName = scope.CityName,
            MinLatitude = scope.Bounds?.South,
            MaxLatitude = scope.Bounds?.North,
            MinLongitude = scope.Bounds?.West,
            MaxLongitude = scope.Bounds?.East,

            // A viewport asks what is inside a rectangle, and a remote provider is not inside
            // anything. Including them — which is right for a radius search, where "I'll do it
            // over video" is a real answer to "who is near me" — would put every remote provider
            // on the platform behind a map the member panned to one street of.
            IncludeRemoteProviders = !scope.IsViewport && filter.IncludeRemoteProviders
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
    /// Every ancestor path of a node, itself included.
    /// <para>
    /// Derived from the path string rather than by walking the catalogue, because that is the
    /// whole point of a materialised path: <c>/gb/london/southwark/peckham/</c> already contains
    /// <c>/gb/</c>, <c>/gb/london/</c> and <c>/gb/london/southwark/</c>, and asking Location for
    /// them would be four round trips to recover information already in hand.
    /// </para>
    /// </summary>
    private static List<string> AncestorPaths(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var ancestors = new List<string>(segments.Length + 1);
        var builder = new System.Text.StringBuilder("/");

        foreach (var segment in segments)
        {
            builder.Append(segment).Append('/');
            ancestors.Add(builder.ToString());
        }

        return ancestors;
    }
}
