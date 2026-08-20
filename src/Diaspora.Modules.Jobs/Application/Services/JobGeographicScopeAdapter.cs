using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// The one place B9 touches geography.
/// <para>
/// The client sends a city, coordinates and a radius in any combination, plus B4's radius
/// sentinels; all of it is handed to <see cref="IGeographicScopeResolver"/> and normalised once.
/// B9 parses no radius, resolves no city name and computes no distance — which is precisely what
/// B4's contract freeze promises verticals, and this class is B9 taking it up.
/// </para>
/// <para>
/// <b>A job is jurisdiction-scoped rather than point-scoped.</b> It has a country and optionally
/// an area, never a coordinate, so applying a scope narrows by country and area path rather than
/// by distance. The scope's own resolution is still B4's: "near me" means whatever B4 says it
/// means, identically to every other vertical.
/// </para>
/// <para>
/// <b>A remote posting is deliberately not excluded by a geographic scope.</b> Narrowing to
/// "jobs near Manchester" and hiding every remote role a Manchester resident could take would
/// answer a question nobody asked — which is the single most common way a job board's location
/// filter frustrates the people this platform serves.
/// </para>
/// </summary>
public sealed class JobGeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;

    public JobGeographicScopeAdapter(IGeographicScopeResolver resolver) => _resolver = resolver;

    /// <summary>
    /// Resolves a request's geography and narrows a search request with the answer, without any
    /// B4 type crossing back out.
    /// <para>
    /// <b>The signature is the point.</b> <c>JobsArchitectureTests.Only_one_class_talks_to_location</c>
    /// permits exactly this class to know Location exists, and a method handing a
    /// <c>GeographicScope</c> back to a handler would make the handler know it too. Raw values in,
    /// a narrowed <see cref="JobSearchRequest"/> out.
    /// </para>
    /// <para>
    /// A scope that fails to resolve narrows nothing rather than failing the browse — the same
    /// leniency this class already applies to an unknown city name, and for the same reason.
    /// </para>
    /// </summary>
    public async Task<JobSearchRequest> NarrowAsync(
        JobSearchRequest search,
        string? cityName,
        string? countryCode,
        Guid? areaId,
        string? bounds,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var scope = await ResolveAsync(
            cityName, countryCode, areaId,
            latitude: null, longitude: null, radiusKm: null, bounds,
            viewerId, cancellationToken);

        if (scope.IsFailure)
        {
            return search;
        }

        var narrowed = Apply(new JobQueryFilter(), scope.Value);

        return search with
        {
            CountryCode = narrowed.CountryCode ?? search.CountryCode,
            AreaPathPrefix = narrowed.AreaPathPrefix ?? search.AreaPathPrefix,
            AreaPathAncestors = narrowed.AreaPathAncestors,

            // A viewport supersedes the city string: the member has moved the map, and filtering
            // by the name they typed before they moved it answers neither question.
            CityName = narrowed.AreaPathPrefix is not null ? null : search.CityName
        };
    }

    /// <summary>Resolves whatever geography a request carried, including a map viewport.</summary>
    private async Task<Result<GeographicScope>> ResolveAsync(
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
                // than fail the browse. A jobs screen that errors because somebody typed
                // "Manchestor" is worse than one that shows the country's jobs.
                StrictCityResolution = false
            },
            cancellationToken);

    /// <summary>
    /// Turns a resolved scope into the geographic half of a posting filter.
    /// <para>
    /// Country and area path, because that is what the posting table indexes — jurisdiction, not
    /// geometry. An unbounded scope narrows nothing, which is "everywhere" rather than "nowhere",
    /// per B4's own flag.
    /// </para>
    /// </summary>
    private static JobQueryFilter Apply(JobQueryFilter filter, GeographicScope scope)
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

        // A viewport. A posting is at a jurisdiction, not at a point, so the rectangle is
        // answered as the smallest catalogue subtree containing it — plus the subtree's own
        // ancestors, or every job filed against London disappears the moment somebody zooms in.
        // Deliberately approximate, and the alternative is an empty screen.
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

        // B4's materialised path, matched as a prefix. The posting stores the same string
        // precisely so this is an indexed LIKE rather than a hierarchy walk.
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
}
