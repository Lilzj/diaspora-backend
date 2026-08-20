using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;

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
    private readonly ICountryRepository _countries;
    private readonly GeospatialProviderRegistry _spatial;
    private readonly IGeoDistanceCalculator _calculator;

    public LocationDirectory(
        IAdministrativeAreaRepository areas,
        ICountryRepository countries,
        GeospatialProviderRegistry spatial,
        IGeoDistanceCalculator calculator)
    {
        _areas = areas;
        _countries = countries;
        _spatial = spatial;
        _calculator = calculator;
    }

    public Task<Country?> FindCountryAsync(
        CountryCode code, CancellationToken cancellationToken = default) =>
        _countries.GetByCodeAsync(code, cancellationToken);

    public Task<AdministrativeArea?> FindAreaAsync(
        AreaId areaId, CancellationToken cancellationToken = default) =>
        _areas.GetByIdAsync(areaId, cancellationToken);

    public async Task<IReadOnlyDictionary<AreaId, AdministrativeArea>> FindAreasAsync(
        IReadOnlyCollection<AreaId> areaIds, CancellationToken cancellationToken = default)
    {
        var areas = await _areas.GetManyAsync(areaIds, cancellationToken);
        return areas.ToDictionary(area => area.Id);
    }

    public async Task<IReadOnlyList<AdministrativeArea>> GetBreadcrumbAsync(
        AreaId areaId, CancellationToken cancellationToken = default)
    {
        var area = await _areas.GetByIdAsync(areaId, cancellationToken);

        if (area is null)
        {
            return [];
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        // Root first, and including the node itself — a breadcrumb without its leaf is a
        // breadcrumb the caller has to reassemble.
        return [.. ancestors, area];
    }

    public async Task<AdministrativeArea?> FindNearestAreaAsync(
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

        return nearest?.Area;
    }

    public Distance DistanceBetween(GeoCoordinate origin, GeoCoordinate destination) =>
        _calculator.Between(origin, destination);

    /// <summary>
    /// The radius the nearest-area probe starts from. Wide enough that a listing pinned in
    /// open country still resolves to the town it belongs to, narrow enough that one pinned in
    /// the sea resolves to nothing rather than to a coastline four hundred kilometres away.
    /// </summary>
    private const double CatalogSearchRadiusKm = 150d;
}
