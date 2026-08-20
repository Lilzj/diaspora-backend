using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Search;

/// <summary>
/// Serves place-name autocomplete.
/// <para>
/// Ranking happens here rather than in SQL, and deliberately. "Exact beats prefix beats
/// substring, then proximity, then prominence" is a product decision about what a member
/// means when they type three letters; expressing it as a translatable <c>ORDER BY</c> would
/// either be provider-specific SQL or a CASE expression no index can serve. The repository
/// over-fetches a bounded candidate set and the domain decides the order.
/// </para>
/// </summary>
public sealed class SearchLocationsQueryHandler
    : IQueryHandler<SearchLocationsQuery, LocationSearchResponse>
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;
    private readonly IGeoDistanceCalculator _calculator;

    public SearchLocationsQueryHandler(
        IAdministrativeAreaRepository areas,
        ICountryRepository countries,
        IGeoDistanceCalculator calculator)
    {
        _areas = areas;
        _countries = countries;
        _calculator = calculator;
    }

    public async Task<Result<LocationSearchResponse>> Handle(
        SearchLocationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var countryCode = ParseCountry(request.CountryCode);
        var near = ParseCoordinate(request.NearLatitude, request.NearLongitude);

        var query = LocationTextQuery.Create(
            request.Term, request.Levels, countryCode, near, request.Limit);

        if (query.IsFailure)
        {
            return Result.Failure<LocationSearchResponse>(query.Error);
        }

        LocationTelemetry.LocationSearches.Add(1);

        var candidates = await _areas.SearchAsync(query.Value, cancellationToken);

        var ranked = candidates
            .Select(area => new
            {
                Area = area,
                Score = query.Value.Score(area.Name),
                Distance = near is not null && area.Center is not null
                    ? _calculator.Between(near, area.Center).Kilometers
                    : double.MaxValue
            })
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)

            // Proximity before prominence: a member in Manchester typing "sal" means Salford,
            // not the more prominent place of the same name three hundred kilometres away.
            .ThenBy(scored => scored.Distance)
            .ThenByDescending(scored => scored.Area.Prominence)
            .Take(query.Value.Limit)
            .ToList();

        if (ranked.Count == 0)
        {
            // The ratio of this to total searches is how the catalogue's coverage gap becomes
            // visible before members start reporting that their city is missing.
            LocationTelemetry.EmptySearches.Add(1);

            return Result.Success(new LocationSearchResponse
            {
                Term = query.Value.Term,
                Results = []
            });
        }

        var countries = await LoadCountriesAsync(ranked.Select(r => r.Area), cancellationToken);
        var results = new List<LocationReferenceResponse>(ranked.Count);

        foreach (var scored in ranked)
        {
            var country = countries.GetValueOrDefault(scored.Area.CountryCode.Value);

            if (country is null)
            {
                continue;
            }

            var ancestors = await _areas.GetAncestorsAsync(scored.Area, cancellationToken);

            results.Add(LocationMapper.ToReference(scored.Area, ancestors, country));
        }

        return Result.Success(new LocationSearchResponse
        {
            Term = query.Value.Term,
            Results = results
        });
    }

    private async Task<Dictionary<string, Country>> LoadCountriesAsync(
        IEnumerable<AdministrativeArea> areas, CancellationToken cancellationToken)
    {
        var codes = areas
            .Select(area => area.CountryCode)
            .DistinctBy(code => code.Value)
            .ToList();

        var countries = new Dictionary<string, Country>(StringComparer.Ordinal);

        foreach (var code in codes)
        {
            var country = await _countries.GetByCodeAsync(code, cancellationToken);

            if (country is not null)
            {
                countries[code.Value] = country;
            }
        }

        return countries;
    }

    private static CountryCode? ParseCountry(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : CountryCode.TryCreate(value) is { IsSuccess: true } parsed ? parsed.Value : null;

    private static GeoCoordinate? ParseCoordinate(double? latitude, double? longitude) =>
        latitude is null || longitude is null
            ? null
            : GeoCoordinate.TryCreate(latitude.Value, longitude.Value) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;
}
