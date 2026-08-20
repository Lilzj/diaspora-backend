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
/// <b>A listing is jurisdiction-and-area scoped rather than point-scoped, and that is a privacy
/// decision as much as a modelling one.</b> A second-hand item is at somebody's home; storing a
/// coordinate would mean the platform holds where a member lives, and rendering one would publish
/// it. Narrowing therefore happens by country and by B4's materialised area path, which is as
/// precise as "Peckham" and no more.
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

    public MarketplaceGeographicScopeAdapter(IGeographicScopeResolver resolver) =>
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
                // than fail the browse. A marketplace that errors because somebody typed
                // "Manchestor" is worse than one that shows the country's listings.
                StrictCityResolution = false
            },
            cancellationToken);

    /// <summary>
    /// Turns a resolved scope into the geographic half of a listing filter.
    /// <para>
    /// Country and area path, because that is what the listing table indexes. An unbounded scope
    /// narrows nothing, which is "everywhere" rather than "nowhere", per B4's own flag.
    /// </para>
    /// </summary>
    public static ListingQueryFilter Apply(ListingQueryFilter filter, GeographicScope scope)
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

        // A viewport. This module deliberately stores no coordinate — a second-hand item is at
        // somebody's home, not at a published point — so a rectangle becomes the smallest
        // catalogue subtree containing it, plus that subtree's ancestors.
        if (scope is { IsViewport: true, ViewportPath: { } viewport })
        {
            return narrowed with
            {
                AreaPathPrefix = viewport.Value,
                AreaPathAncestors = scope.ViewportAncestorPaths
                    .Select(ancestor => ancestor.Value)
                    .ToList()
            };
        }

        // B4's materialised path, matched as a prefix. The listing stores the same string precisely
        // so this is an indexed LIKE rather than a hierarchy walk.
        if (scope.Path is { } path)
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
