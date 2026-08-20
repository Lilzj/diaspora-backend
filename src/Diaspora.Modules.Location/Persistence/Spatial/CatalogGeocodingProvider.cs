using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Persistence.Spatial;

/// <summary>
/// Geocoding against the platform's own catalogue.
/// <para>
/// The default, and it calls nobody. The frontend currently reverse-geocodes by sending every
/// prospective member's IP address to <c>ipapi.co</c> from the device during registration — an
/// unvetted third party, no agreement, no rate limit, no way to cache. Answering from the
/// catalogue is worse at street precision and better at everything else: it is free, it is
/// fast, it cannot be down, and it discloses nothing.
/// </para>
/// <para>
/// It is deliberately honest about its limits. A coordinate in a country the catalogue has no
/// areas for resolves to the country and no further, rather than to the nearest node a
/// thousand kilometres away — <see cref="MaxReverseGeocodeRadiusKm"/> is where "nearest" stops
/// meaning anything. A Mapbox or Places provider registering under a different key replaces
/// this for deployments that need building-level accuracy.
/// </para>
/// </summary>
public sealed class CatalogGeocodingProvider : IGeocodingProvider
{
    /// <summary>
    /// Beyond this, the nearest catalogue node is not where the member is. Roughly the radius
    /// within which a city is a plausible answer for a rural coordinate.
    /// </summary>
    public const double MaxReverseGeocodeRadiusKm = 150d;

    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;
    private readonly IGeospatialQueryProvider _spatial;
    private readonly IGeoDistanceCalculator _calculator;

    public CatalogGeocodingProvider(
        IAdministrativeAreaRepository areas,
        ICountryRepository countries,
        IGeospatialQueryProvider spatial,
        IGeoDistanceCalculator calculator)
    {
        _areas = areas;
        _countries = countries;
        _spatial = spatial;
        _calculator = calculator;
    }

    public string Key => "catalog";

    public bool IsExternal => false;

    public async Task<Result<LocationSnapshot>> ReverseGeocodeAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var radius = Domain.ValueObjects.SearchRadius.FromKilometers(MaxReverseGeocodeRadiusKm);

        if (radius.IsFailure)
        {
            return Result.Failure<LocationSnapshot>(radius.Error);
        }

        // Neighbourhood first: it is the rung the product is built around, and a member
        // standing in Peckham should be told Peckham rather than London.
        var search = NearbySearch.Create(
            coordinate,
            radius.Value,
            _calculator,
            levels: [AdministrativeLevel.Neighborhood, AdministrativeLevel.City],
            limit: 1);

        if (search.IsFailure)
        {
            return Result.Failure<LocationSnapshot>(search.Error);
        }

        var nearest = await _spatial.FindNearestAsync(search.Value, cancellationToken);

        if (nearest is null)
        {
            return Result.Failure<LocationSnapshot>(LocationErrors.GeocodingUnavailable);
        }

        return await BuildSnapshotAsync(nearest.Area, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LocationSnapshot>>> ForwardGeocodeAsync(
        string query, CountryCode? within, CancellationToken cancellationToken = default)
    {
        var textQuery = LocationTextQuery.Create(query, countryCode: within);

        if (textQuery.IsFailure)
        {
            return Result.Failure<IReadOnlyList<LocationSnapshot>>(textQuery.Error);
        }

        var candidates = await _areas.SearchAsync(textQuery.Value, cancellationToken);

        var ranked = candidates
            .Select(area => new { Area = area, Score = textQuery.Value.Score(area.Name) })
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .ThenByDescending(scored => scored.Area.Prominence)
            .Take(textQuery.Value.Limit)
            .Select(scored => scored.Area)
            .ToList();

        var snapshots = new List<LocationSnapshot>(ranked.Count);

        foreach (var area in ranked)
        {
            var snapshot = await BuildSnapshotAsync(area, cancellationToken);

            if (snapshot.IsSuccess)
            {
                snapshots.Add(snapshot.Value);
            }
        }

        return Result.Success<IReadOnlyList<LocationSnapshot>>(snapshots);
    }

    /// <summary>
    /// Turns a catalogue node into a standalone snapshot by reading its ancestors off the path.
    /// <para>
    /// The city and state are filled from the breadcrumb rather than left null, because the
    /// snapshot has to render on its own — a saved location that shows a neighbourhood with no
    /// city reads as broken data even when the link back to the catalogue is intact.
    /// </para>
    /// </summary>
    private async Task<Result<LocationSnapshot>> BuildSnapshotAsync(
        AdministrativeArea area, CancellationToken cancellationToken)
    {
        var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);

        if (country is null)
        {
            return Result.Failure<LocationSnapshot>(
                LocationErrors.CountryNotFound(area.CountryCode.Value));
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        var city = area.Level == AdministrativeLevel.City
            ? area
            : ancestors.FirstOrDefault(node => node.Level == AdministrativeLevel.City);

        var state = ancestors.FirstOrDefault(
            node => node.Level is AdministrativeLevel.StateProvince or AdministrativeLevel.Region);

        var neighborhood = area.Level == AdministrativeLevel.Neighborhood ? area : null;

        return LocationSnapshot.TryCreate(
            country.Code,
            country.Name.Value,
            state?.Name.Value,
            city?.Name.Value,
            neighborhood?.Name.Value,
            street: null,
            area.Center,
            area.Id);
    }
}
