using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Turns whatever geography a client sent into a scope a vertical can filter with.
/// <para>
/// This is where the platform's geographic ambiguity is resolved, once. Read the resolution
/// order below and note what each step is doing: it is the code every vertical would
/// otherwise write, and the reason none of them will have to.
/// </para>
/// </summary>
public sealed class GeographicScopeResolver : IGeographicScopeResolver
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ILocationProfileRepository _profiles;
    private readonly GeospatialProviderRegistry _spatial;
    private readonly IGeoDistanceCalculator _calculator;

    public GeographicScopeResolver(
        IAdministrativeAreaRepository areas,
        ILocationProfileRepository profiles,
        GeospatialProviderRegistry spatial,
        IGeoDistanceCalculator calculator)
    {
        _areas = areas;
        _profiles = profiles;
        _spatial = spatial;
        _calculator = calculator;
    }

    public async Task<Result<GeographicScope>> ResolveAsync(
        GeographicScopeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var radius = SearchRadius.FromClientValue(request.RadiusKm);

        if (radius.IsFailure)
        {
            return Result.Failure<GeographicScope>(radius.Error);
        }

        var countryCode = ParseCountry(request.CountryCode);

        // 0. A map viewport. First, because it is the most explicit spatial statement a client
        //    can make and because a map screen sends its centre alongside its box — taking the
        //    centre would answer a question about a circle that nobody asked.
        if (!string.IsNullOrWhiteSpace(request.Bounds))
        {
            return await BuildFromBoundsAsync(
                request.Bounds, radius.Value, countryCode, cancellationToken);
        }

        // 1. An explicit area id. The client already disambiguated; nothing here can improve
        //    on that, and second-guessing it would override a deliberate choice.
        if (request.AreaId is not null)
        {
            var area = await _areas.GetByIdAsync(request.AreaId, cancellationToken);

            if (area is not null)
            {
                return await BuildFromAreaAsync(area, radius.Value, cancellationToken);
            }
        }

        // 2. Coordinates. A point is unambiguous where a name never is.
        if (request.Latitude is not null && request.Longitude is not null)
        {
            var coordinate = GeoCoordinate.TryCreate(request.Latitude.Value, request.Longitude.Value);

            if (coordinate.IsFailure)
            {
                return Result.Failure<GeographicScope>(coordinate.Error);
            }

            return await BuildFromCoordinateAsync(
                coordinate.Value, radius.Value, countryCode, request.CityName, cancellationToken);
        }

        // 3. A city name, resolved against the catalogue. This is the parameter six of the
        //    frontend's eight vertical calls actually send.
        if (!string.IsNullOrWhiteSpace(request.CityName))
        {
            var scope = await BuildFromCityNameAsync(
                request.CityName, radius.Value, countryCode, cancellationToken);

            if (scope is not null)
            {
                return Result.Success(scope);
            }

            if (request.StrictCityResolution)
            {
                return Result.Failure<GeographicScope>(Domain.Errors.LocationErrors.AreaNotFound);
            }

            // Unresolved but not strict: keep the name so a vertical still filtering on the
            // denormalised string can use it, and keep the country if one was given. Dropping
            // it would silently widen the query to everywhere.
            return Result.Success(new GeographicScope
            {
                CountryCode = countryCode,
                CityName = request.CityName.Trim(),
                Radius = radius.Value
            });
        }

        // 4. The member's own active location — which is what "near me" means, and where
        //    travel mode is honoured for every vertical without any of them knowing it exists.
        if (request.UserId is not null)
        {
            var scope = await BuildFromProfileAsync(
                request.UserId, radius.Value, cancellationToken);

            if (scope is not null)
            {
                return Result.Success(scope);
            }
        }

        // 5. A country, if that is all there was.
        if (countryCode is not null)
        {
            return Result.Success(new GeographicScope
            {
                CountryCode = countryCode,
                Path = HierarchyPath.ForRoot(countryCode.Value),
                Radius = radius.Value
            });
        }

        // Nothing narrowed it. Unbounded rather than an error: browsing without a location is
        // a legitimate request, and a vertical that received a failure here would have to
        // special-case the most common case on the platform.
        return Result.Success(GeographicScope.Unbounded);
    }

    /// <summary>
    /// Resolves a map viewport.
    /// <para>
    /// <b>The box is the answer, and the catalogue nodes are a translation of it.</b> A vertical
    /// that stores a coordinate filters by the rectangle and needs nothing else. A vertical that
    /// files rows against an area cannot use a rectangle at all, so the same box is resolved once
    /// to the nodes it contains — and to the node it sits inside, because zooming into one street
    /// of London must not lose every row filed against London.
    /// </para>
    /// <para>
    /// <b>No <c>Path</c> and no <c>AreaId</c> are set.</b> Both mean "narrow to this subtree", and
    /// a viewport is not a subtree; setting either would make a vertical apply a second, coarser
    /// filter on top of the rectangle and quietly drop rows on the near side of a boundary.
    /// </para>
    /// </summary>
    private async Task<Result<GeographicScope>> BuildFromBoundsAsync(
        string bounds,
        SearchRadius radius,
        CountryCode? countryCode,
        CancellationToken cancellationToken)
    {
        var box = BoundingBox.TryParse(bounds);

        if (box.IsFailure)
        {
            return Result.Failure<GeographicScope>(box.Error);
        }

        if (!_spatial.Supports(GeospatialCapability.BoundingBoxSearch))
        {
            return Result.Failure<GeographicScope>(
                Domain.Errors.LocationErrors.SpatialCapabilityUnsupported(
                    "bounding-box search", _spatial.Active.Key));
        }

        // The span cap lives on the search, not on the box, and it is what stops "the whole
        // world" being a legitimate viewport question that reads every row in the catalogue.
        var search = BoundingBoxSearch.Create(
            box.Value,
            levels: [AdministrativeLevel.Neighborhood, AdministrativeLevel.City],
            countryCode: countryCode,
            limit: ViewportPathLimit);

        if (search.IsFailure)
        {
            return Result.Failure<GeographicScope>(search.Error);
        }

        var inside = await _spatial.Active.FindWithinBoundsAsync(search.Value, cancellationToken);

        var paths = new List<string>(inside.Count + 1);

        foreach (var result in inside)
        {
            paths.Add(result.Area.Path.Value);
        }

        var center = box.Value.Center;

        // The node the viewport sits inside. Resolved from the centre because that is the one
        // point a rectangle always has, and it is what supplies the country when the box
        // contains no catalogued node at all — open sea, farmland, a city nobody has imported.
        AdministrativeArea? containing = null;

        var containingSearch = NearbySearch.Create(
            center,
            SearchRadius.Default,
            _calculator,
            levels: [AdministrativeLevel.Neighborhood, AdministrativeLevel.City],
            countryCode: countryCode,
            limit: 1);

        if (containingSearch.IsSuccess)
        {
            containing = (await _spatial.Active.FindNearestAsync(
                containingSearch.Value, cancellationToken))?.Area;
        }

        if (containing is not null)
        {
            paths.Add(containing.Path.Value);
        }

        var city = containing?.Level == AdministrativeLevel.City ? containing : null;

        var subtree = CommonPathPrefix(paths);

        return Result.Success(new GeographicScope
        {
            CountryCode = countryCode
                ?? containing?.CountryCode
                ?? (inside.Count > 0 ? inside[0].Area.CountryCode : null),
            CityName = city?.Name.Value,
            Center = center,
            Radius = radius,
            Bounds = box.Value,
            IsViewport = true,
            ViewportPath = subtree,
            ViewportAncestorPaths = AncestorsOf(subtree)
        });
    }

    /// <summary>
    /// How many catalogue nodes a viewport search reads before deciding the subtree.
    /// <para>
    /// A cap rather than a page: the answer is a single prefix, so reading more nodes only ever
    /// makes it shorter, and a viewport wide enough to hold more than this has already widened to
    /// a country.
    /// </para>
    /// </summary>
    private const int ViewportPathLimit = 25;

    /// <summary>
    /// The deepest path every node shares, on segment boundaries.
    /// <para>
    /// Segment-wise rather than character-wise, and the distinction is not academic: the common
    /// character prefix of <c>/gb/lon/</c> and <c>/gb/london/</c> is <c>/gb/lon</c>, which is a
    /// prefix no node has and which <c>LIKE '/gb/lon%'</c> would match both of anyway — quietly
    /// admitting a place the member is not looking at.
    /// </para>
    /// </summary>
    private static HierarchyPath? CommonPathPrefix(List<string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var shared = paths[0].Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        for (var index = 1; index < paths.Count && shared.Count > 0; index++)
        {
            var segments = paths[index].Split('/', StringSplitOptions.RemoveEmptyEntries);
            var common = 0;

            while (common < shared.Count &&
                   common < segments.Length &&
                   string.Equals(shared[common], segments[common], StringComparison.Ordinal))
            {
                common++;
            }

            shared.RemoveRange(common, shared.Count - common);
        }

        if (shared.Count == 0)
        {
            return null;
        }

        // No trailing separator: HierarchyPath.Value never carries one, and a prefix that did
        // would match nothing at all against a stored path.
        var value = "/" + string.Join('/', shared);

        return HierarchyPath.TryCreate(value) is { IsSuccess: true } path ? path.Value : null;
    }

    /// <summary>
    /// Every ancestor path of a node, itself included, derived from the string rather than by
    /// walking the catalogue — the path already contains them.
    /// </summary>
    private static List<HierarchyPath> AncestorsOf(HierarchyPath? path)
    {
        if (path is null)
        {
            return [];
        }

        var segments = path.Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var ancestors = new List<HierarchyPath>(segments.Length);
        var builder = new System.Text.StringBuilder();

        foreach (var segment in segments)
        {
            builder.Append('/').Append(segment);

            if (HierarchyPath.TryCreate(builder.ToString()) is { IsSuccess: true } ancestor)
            {
                ancestors.Add(ancestor.Value);
            }
        }

        return ancestors;
    }

    private async Task<Result<GeographicScope>> BuildFromAreaAsync(
        AdministrativeArea area, SearchRadius radius, CancellationToken cancellationToken)
    {
        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        var city = area.Level == AdministrativeLevel.City
            ? area
            : ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        // The scope path is what a city- or country-wide radius actually resolves to: the
        // ancestor whose whole subtree the member asked for.
        var scopePath = radius.Scope switch
        {
            RadiusScope.City => (city ?? area).Path,
            RadiusScope.Country => HierarchyPath.ForRoot(area.CountryCode.Value),
            _ => area.Path
        };

        NearbySearch? search = null;

        if (area.Center is not null)
        {
            var built = NearbySearch.Create(
                area.Center, radius, _calculator,
                countryCode: area.CountryCode,
                withinPath: radius.Scope == RadiusScope.Radius ? null : scopePath);

            search = built.IsSuccess ? built.Value : null;
        }

        return Result.Success(new GeographicScope
        {
            CountryCode = area.CountryCode,
            AreaId = area.Id,
            Path = scopePath,
            CityName = city?.Name.Value,
            Center = area.Center,
            Radius = radius,
            Bounds = search?.Bounds,
            Search = search
        });
    }

    private async Task<Result<GeographicScope>> BuildFromCoordinateAsync(
        GeoCoordinate coordinate,
        SearchRadius radius,
        CountryCode? countryCode,
        string? cityName,
        CancellationToken cancellationToken)
    {
        // Resolve the point to a node so the scope carries a path and a city name as well as a
        // circle. A vertical that indexes by area rather than by coordinate can then answer
        // without doing any geometry at all — which is most of them, today.
        var nearestSearch = NearbySearch.Create(
            coordinate,
            SearchRadius.Default,
            _calculator,
            levels: [AdministrativeLevel.Neighborhood, AdministrativeLevel.City],
            countryCode: countryCode,
            limit: 1);

        AdministrativeArea? resolved = null;

        if (nearestSearch.IsSuccess)
        {
            var nearest = await _spatial.Active.FindNearestAsync(nearestSearch.Value, cancellationToken);
            resolved = nearest?.Area;
        }

        var ancestors = resolved is null
            ? []
            : await _areas.GetAncestorsAsync(resolved, cancellationToken);

        var city = resolved?.Level == AdministrativeLevel.City
            ? resolved
            : ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        var scopePath = radius.Scope switch
        {
            RadiusScope.City => city?.Path,
            RadiusScope.Country when resolved is not null =>
                HierarchyPath.ForRoot(resolved.CountryCode.Value),
            RadiusScope.Country when countryCode is not null =>
                HierarchyPath.ForRoot(countryCode.Value),
            _ => null
        };

        var search = NearbySearch.Create(
            coordinate,
            radius,
            _calculator,
            countryCode: resolved?.CountryCode ?? countryCode,
            withinPath: scopePath);

        return Result.Success(new GeographicScope
        {
            CountryCode = resolved?.CountryCode ?? countryCode,
            AreaId = resolved?.Id,
            Path = scopePath ?? resolved?.Path,
            CityName = city?.Name.Value ?? cityName?.Trim(),
            Center = coordinate,
            Radius = radius,
            Bounds = search.IsSuccess ? search.Value.Bounds : null,
            Search = search.IsSuccess ? search.Value : null
        });
    }

    private async Task<GeographicScope?> BuildFromCityNameAsync(
        string cityName,
        SearchRadius radius,
        CountryCode? countryCode,
        CancellationToken cancellationToken)
    {
        var slug = LocationSlug.TryCreate(cityName);

        if (slug.IsFailure)
        {
            return null;
        }

        var city = await _areas.GetBySlugAsync(
            slug.Value, AdministrativeLevel.City, countryCode, cancellationToken);

        if (city is null)
        {
            return null;
        }

        var scope = await BuildFromAreaAsync(city, radius, cancellationToken);

        return scope.IsSuccess ? scope.Value : null;
    }

    private async Task<GeographicScope?> BuildFromProfileAsync(
        UserId userId, SearchRadius radius, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByUserAsync(userId, cancellationToken);
        var active = profile?.ActiveLocation;

        if (active is null)
        {
            return null;
        }

        // The member's own preferred radius wins when the request did not name one. A client
        // that has already stored "city-wide" should not silently get five kilometres because
        // this particular screen forgot to send the parameter.
        var effectiveRadius = radius == SearchRadius.Default && profile is not null
            ? profile.Preferences.SearchRadius
            : radius;

        if (active.AreaId is not null)
        {
            var area = await _areas.GetByIdAsync(active.AreaId, cancellationToken);

            if (area is not null)
            {
                var scope = await BuildFromAreaAsync(area, effectiveRadius, cancellationToken);
                return scope.IsSuccess ? scope.Value : null;
            }
        }

        if (active.Coordinate is not null)
        {
            var scope = await BuildFromCoordinateAsync(
                active.Coordinate,
                effectiveRadius,
                active.CountryCode,
                active.CityName,
                cancellationToken);

            return scope.IsSuccess ? scope.Value : null;
        }

        return new GeographicScope
        {
            CountryCode = active.CountryCode,
            CityName = active.CityName,
            Path = HierarchyPath.ForRoot(active.CountryCode.Value),
            Radius = effectiveRadius
        };
    }

    private static CountryCode? ParseCountry(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : CountryCode.TryCreate(value) is { IsSuccess: true } parsed ? parsed.Value : null;
}
