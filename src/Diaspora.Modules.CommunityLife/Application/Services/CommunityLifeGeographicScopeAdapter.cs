using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
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
    /// Narrows an event filter to whatever geography the browse carried.
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
        Guid? areaId,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAsync(
            cityName, countryCode, areaId, bounds, viewerId, cancellationToken);

        return scope.IsFailure
            ? Result.Failure<EventQueryFilter>(scope.Error)
            : Result.Success(Apply(filter, scope.Value));
    }

    /// <summary>Narrows an organization filter to whatever geography the browse carried.</summary>
    public async Task<Result<OrganizationQueryFilter>> NarrowAsync(
        OrganizationQueryFilter filter,
        string? cityName,
        string? countryCode,
        Guid? areaId,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        var scope = await ResolveAsync(
            cityName, countryCode, areaId, bounds, viewerId, cancellationToken);

        return scope.IsFailure
            ? Result.Failure<OrganizationQueryFilter>(scope.Error)
            : Result.Success(Apply(filter, scope.Value));
    }

    /// <summary>Resolves whatever geography a browse request carried.</summary>
    private async Task<Result<GeographicScope>> ResolveAsync(
        string? cityName,
        string? countryCode,
        Guid? areaId,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default) =>
        await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = cityName,
                CountryCode = countryCode,
                AreaId = areaId is { } id && id != Guid.Empty ? AreaId.From(id) : null,
                Bounds = bounds,
                UserId = viewerId,

                // Lenient: a city name the catalogue does not know should narrow nothing rather
                // than fail the browse. Somebody looking for their community in a place they have
                // just arrived in will spell it the way they heard it.
                StrictCityResolution = false
            },
            cancellationToken);

    /// <summary>
    /// Narrows an event filter to a viewport.
    /// <para>
    /// The rectangle only — no path and no city on top of it. B4 leaves both null for a viewport
    /// precisely so an event held just over a boundary is not dropped from a map that plainly
    /// shows the venue.
    /// </para>
    /// </summary>
    private static EventQueryFilter Apply(EventQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope is not { IsViewport: true, Bounds: { } bounds })
        {
            return filter;
        }

        return filter with
        {
            CountryCode = scope.CountryCode?.Value ?? filter.CountryCode,
            CityName = null,
            AreaPathPrefix = null,
            NeighborhoodAreaId = null,
            BoundsSouth = bounds.South,
            BoundsNorth = bounds.North,
            BoundsWest = bounds.West,
            BoundsEast = bounds.East
        };
    }

    /// <summary>Narrows an organization filter to the catalogue subtree a viewport resolved to.</summary>
    private static OrganizationQueryFilter Apply(
        OrganizationQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope is not { IsViewport: true, ViewportPath: { } viewport })
        {
            return filter;
        }

        return filter with
        {
            CountryCode = scope.CountryCode?.Value ?? filter.CountryCode,
            CityName = null,
            NeighborhoodAreaId = null,
            AreaPathPrefix = viewport.Value,
            AreaPathAncestors = scope.ViewportAncestorPaths
                .Select(ancestor => ancestor.Value)
                .ToList()
        };
    }
}
