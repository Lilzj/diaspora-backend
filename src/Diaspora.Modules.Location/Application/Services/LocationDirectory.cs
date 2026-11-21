using Diaspora.Contracts.Location.V1;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// The read surface other modules look geography up through.
/// <para>
/// Thin by design. Every method is either a repository call or a call into the active spatial
/// provider; there is no logic here that a consumer could have written for itself, which is
/// exactly what makes the interface safe to freeze.
/// </para>
/// </summary>
public sealed class LocationDirectory : ILocationDirectory
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly IAdministrativeAreaSearch _areaSearch;
    private readonly ICountryRepository _countries;
    private readonly GeospatialProviderRegistry _spatial;
    private readonly IGeoDistanceCalculator _calculator;

    public LocationDirectory(
        IAdministrativeAreaRepository areas,
        IAdministrativeAreaSearch areaSearch,
        ICountryRepository countries,
        GeospatialProviderRegistry spatial,
        IGeoDistanceCalculator calculator)
    {
        _areas = areas;
    _areaSearch = areaSearch;
        _countries = countries;
        _spatial = spatial;
        _calculator = calculator;
    }

    public async Task<CountryDescriptor?> FindCountryAsync(
        CountryCode code, CancellationToken cancellationToken = default) =>
        Describe(await _countries.GetByCodeAsync(code, cancellationToken));

    public async Task<AreaDescriptor?> FindAreaAsync(
        AreaId areaId, CancellationToken cancellationToken = default) =>
        Describe(await _areas.GetByIdAsync(areaId, cancellationToken));

    public async Task<IReadOnlyDictionary<AreaId, AreaDescriptor>> FindAreasAsync(
        IReadOnlyCollection<AreaId> areaIds, CancellationToken cancellationToken = default)
    {
        var areas = await _areas.GetManyAsync(areaIds, cancellationToken);
        return areas.ToDictionary(area => area.Id, area => Describe(area)!);
    }

    public async Task<AreaDescriptor?> FindAreaBySlugAsync(
        LocationSlug slug,
        AdministrativeLevel? level = null,
        CountryCode? countryCode = null,
        CancellationToken cancellationToken = default) =>
        Describe(await _areas.GetBySlugAsync(slug, level, countryCode, cancellationToken));

    public async Task<IReadOnlyList<AreaDescriptor>> GetChildAreasAsync(
        AreaId parentId,
        AdministrativeLevel? level = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // The country comes from the parent rather than from the caller: a consumer holding a
        // projection has no CountryId, and asking it for one would mean a second lookup to
        // answer a question the parent already settles.
        var parent = await _areas.GetByIdAsync(parentId, cancellationToken);

        if (parent is null)
        {
            return [];
        }

        var children = await _areaSearch.GetChildrenAsync(
            parent.CountryId,
            parentId,
            level,
            new PaginationRequest { Page = 1, PageSize = Math.Clamp(limit, 1, 200) },
            cancellationToken);

        return [.. children.Items.Select(area => Describe(area)!)];
    }

    public async Task<IReadOnlyList<AreaDescriptor>> GetBreadcrumbAsync(
        AreaId areaId, CancellationToken cancellationToken = default)
    {
        var area = await _areas.GetByIdAsync(areaId, cancellationToken);

        if (area is null)
        {
            return [];
        }

        var ancestors = await _areaSearch.GetAncestorsAsync(area, cancellationToken);

        // Root first, and including the node itself — a breadcrumb without its leaf is a
        // breadcrumb the caller has to reassemble.
        return [.. ancestors.Select(node => Describe(node)!), Describe(area)!];
    }

    public async Task<AreaDescriptor?> FindNearestAreaAsync(
        GeoCoordinate coordinate,
        AdministrativeLevel? level = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var radius = SearchRadius.FromKilometers(CatalogSearchRadiusKm);

        if (radius.IsFailure)
        {
            return null;
        }

        var search = NearbySearch.Create(
            coordinate,
            radius.Value,
            _calculator,
            levels: level is null ? null : [level.Value],
            limit: 1);

        if (search.IsFailure)
        {
            return null;
        }

        var nearest = await _spatial.Active.FindNearestAsync(search.Value, cancellationToken);

        return Describe(nearest?.Area);
    }

    /// <summary>
    /// The one place an aggregate becomes a projection.
    /// <para>
    /// Null in, null out, so every caller above stays a one-liner and no method has to decide
    /// separately what a missing node means.
    /// </para>
    /// </summary>
    private static AreaDescriptor? Describe(AdministrativeArea? area) =>
        area is null
            ? null
            : new AreaDescriptor(
                area.Id,
                area.Name.Value,
                area.Slug.Value,
                area.Path,
                area.Level,
                area.CountryCode,
                area.ParentId,
                area.Center);

    private static CountryDescriptor? Describe(Country? country) =>
        country is null
            ? null
            : new CountryDescriptor(
                country.Code,
                country.Name.Value,
                country.Slug.Value,
                country.Flag,
                country.IsSupported,
                country.IsFeatured,
                country.Center,
                country.Extent);

    public Distance DistanceBetween(GeoCoordinate origin, GeoCoordinate destination) =>
        _calculator.Between(origin, destination);

    /// <summary>
    /// The radius the nearest-area probe starts from. Wide enough that a listing pinned in
    /// open country still resolves to the town it belongs to, narrow enough that one pinned in
    /// the sea resolves to nothing rather than to a coastline four hundred kilometres away.
    /// </summary>
    private const double CatalogSearchRadiusKm = 150d;
}
