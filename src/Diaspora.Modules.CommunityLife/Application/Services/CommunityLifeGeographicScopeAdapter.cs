using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// The one place B12 turns a client's loose geography into something it can filter with.
/// <para>
/// <b>B12 declared this dependency and never took it up.</b> The module's own description says it
/// consumes <see cref="IGeographicScopeResolver"/>; until this phase nothing in it did, and the
/// browse screens filtered on the raw <c>countryCode</c>, <c>city</c> and <c>neighborhoodAreaId</c>
/// the client happened to send. That is adequate for a list and impossible for a map, which is why
/// a viewport arrives here rather than as four more query parameters.
/// </para>
/// <para>
/// <b>The two aggregates take the same rectangle differently, and the difference is real.</b> An
/// event is at a venue and the venue has a coordinate, so a viewport filters it exactly. An
/// organization is an institution filed against a place — a church is in Peckham, not at
/// 51.4739, -0.0736 — so its rectangle is answered as the catalogue subtree B4 resolved, the same
/// approximation Jobs and Marketplace make and for the same reason.
/// </para>
/// </summary>
public sealed class CommunityLifeGeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;

    public CommunityLifeGeographicScopeAdapter(IGeographicScopeResolver resolver) =>
        _resolver = resolver;

    /// <summary>
    /// Narrows an event filter to whatever geography the browse carried, without the place
    /// catalogue (14 Oct 2026).
    /// <para>
    /// <b>No B4 type crosses back out, and that is enforced.</b>
    /// <c>CommunityLifeArchitectureTests.Location_is_reached_only_from_its_adapters</c> names the
    /// handful of classes permitted to know Location exists; a method returning a
    /// <c>GeographicScope</c> would put every search handler on that list.
    /// </para>
    /// </summary>
    public async Task<Result<EventQueryFilter>> NarrowAsync(
        EventQueryFilter filter,
        string? cityName,
        string? countryCode,
        double? latitude,
        double? longitude,
        double? radiusKm,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAsync(
            cityName, countryCode, latitude, longitude, radiusKm, bounds, viewerId, cancellationToken);

        return scope.IsFailure
            ? Result.Failure<EventQueryFilter>(scope.Error)
            : Result.Success(Apply(filter, scope.Value));
    }

    /// <summary>Narrows an organization filter to whatever geography the browse carried.</summary>
    public async Task<Result<OrganizationQueryFilter>> NarrowAsync(
        OrganizationQueryFilter filter,
        string? cityName,
        string? countryCode,
        double? latitude,
        double? longitude,
        double? radiusKm,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAsync(
            cityName, countryCode, latitude, longitude, radiusKm, bounds, viewerId, cancellationToken);

        return scope.IsFailure
            ? Result.Failure<OrganizationQueryFilter>(scope.Error)
            : Result.Success(Apply(filter, scope.Value));
    }

    /// <summary>Resolves whatever geography a browse request carried, without the catalogue.</summary>
    private async Task<Result<GeographicScope>> ResolveAsync(
        string? cityName,
        string? countryCode,
        double? latitude,
        double? longitude,
        double? radiusKm,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default) =>
        await _resolver.ResolveForDiscoveryAsync(
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

    /// <summary>
    /// Narrows an event filter: a viewport or a circle by the venue's own coordinate (a circle
    /// also keeps events with no venue coordinate in the town it named), a town by its name in
    /// either letter case, a country by the country. No path, no neighbourhood.
    /// </summary>
    private static EventQueryFilter Apply(EventQueryFilter filter, GeographicScope scope)
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
            NeighborhoodAreaId = null
        };

        if (scope.Bounds is { } box && (scope.IsViewport || scope.IsAroundPoint))
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.IsViewport ? null : scope.CityName,
                BoundsSouth = box.South,
                BoundsNorth = box.North,
                BoundsWest = box.West,
                BoundsEast = box.East
            };
        }

        return narrowed with
        {
            CityName = scope.Radius.Scope == RadiusScope.Country ? null : scope.CityName
        };
    }

    /// <summary>Narrows an organization filter, by the same rules as an event filter.</summary>
    private static OrganizationQueryFilter Apply(
        OrganizationQueryFilter filter, GeographicScope scope)
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
            AreaPathAncestors = null,
            NeighborhoodAreaId = null
        };

        if (scope.Bounds is { } box && (scope.IsViewport || scope.IsAroundPoint))
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.IsViewport ? null : scope.CityName,
                MinLatitude = box.South,
                MaxLatitude = box.North,
                MinLongitude = box.West,
                MaxLongitude = box.East
            };
        }

        return narrowed with
        {
            CityName = scope.Radius.Scope == RadiusScope.Country ? null : scope.CityName
        };
    }
}
