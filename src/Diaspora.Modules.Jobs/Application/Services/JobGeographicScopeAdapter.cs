using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;

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
/// <b>14 Oct 2026: no catalogue.</b> A posting has a country, the town its employer typed and,
/// since the same day, an optional pin. A circle is answered by the pin (and by unpinned postings
/// in the town the request named), a viewport by the pin, a town by its name in either letter
/// case, a country by the country. No area id or path narrows anything.
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
    private readonly ILocationDirectory _directory;

    public JobGeographicScopeAdapter(IGeographicScopeResolver resolver, ILocationDirectory directory)
    {
        _resolver = resolver;
        _directory = directory;
    }

    /// <summary>
    /// Resolves a request's geography and narrows a search request with the answer, without any
    /// B4 type crossing back out.
    /// <para>
    /// <b>The signature is the point.</b> <c>JobsArchitectureTests.Only_one_class_talks_to_location</c>
    /// permits exactly this class to know Location exists, and a method handing a
    /// <c>GeographicScope</c> back to a handler would make the handler know it too. Raw values in,
    /// a narrowed <see cref="JobSearchRequest"/> and the circle to trim by out.
    /// </para>
    /// <para>
    /// A scope that fails to resolve narrows nothing rather than failing the browse. The area id
    /// old app builds still send is accepted and ignored.
    /// </para>
    /// </summary>
    public async Task<(JobSearchRequest Search, JobSearchCircle? Circle)> NarrowAsync(
        JobSearchRequest search,
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
        ArgumentNullException.ThrowIfNull(search);
        _ = areaId;

        var scope = await _resolver.ResolveForDiscoveryAsync(
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

        if (scope.IsFailure || scope.Value.IsUnbounded)
        {
            return (search, null);
        }

        var resolved = scope.Value;

        var narrowed = search with
        {
            CountryCode = resolved.CountryCode?.Value ?? search.CountryCode,
            AreaPathPrefix = null,
            AreaPathAncestors = null
        };

        if (resolved is { IsViewport: true, Bounds: { } viewport })
        {
            // A viewport supersedes the town the member typed before they moved the map.
            return (narrowed with
            {
                CityName = null,
                MinLatitude = viewport.South,
                MaxLatitude = viewport.North,
                MinLongitude = viewport.West,
                MaxLongitude = viewport.East
            }, null);
        }

        if (resolved.IsAroundPoint && resolved.Bounds is { } circle && resolved.Center is { } centre)
        {
            return (narrowed with
            {
                CityName = null,
                UnpinnedCityName = resolved.CityName,
                MinLatitude = circle.South,
                MaxLatitude = circle.North,
                MinLongitude = circle.West,
                MaxLongitude = circle.East
            }, new JobSearchCircle(centre.Latitude, centre.Longitude, resolved.Radius.Distance.Kilometers));
        }

        return (narrowed with
        {
            CityName = resolved.Radius.Scope == RadiusScope.Country ? null : resolved.CityName
        }, null);
    }

    /// <summary>
    /// The postings inside a circle: the repository answered the rectangle around it, and this
    /// drops the corners with Location's own distance. An unpinned posting passed only by naming
    /// the requested town, and is kept.
    /// </summary>
    public IReadOnlyList<JobPosting> WithinCircle(
        IReadOnlyList<JobPosting> postings, JobSearchCircle? circle)
    {
        ArgumentNullException.ThrowIfNull(postings);

        if (circle is null
            || GeoCoordinate.TryCreate(circle.Latitude, circle.Longitude) is not { IsSuccess: true } origin)
        {
            return postings;
        }

        return
        [
            .. postings.Where(posting =>
                posting.Latitude is not { } lat
                || posting.Longitude is not { } lng
                || GeoCoordinate.TryCreate(lat, lng) is not { IsSuccess: true } point
                || _directory.DistanceBetween(origin.Value, point.Value).Kilometers <= circle.RadiusKm)
        ];
    }
}

/// <summary>The circle a search was made around, so the handler can trim the rectangle's corners.</summary>
public sealed record JobSearchCircle(double Latitude, double Longitude, double RadiusKm);
