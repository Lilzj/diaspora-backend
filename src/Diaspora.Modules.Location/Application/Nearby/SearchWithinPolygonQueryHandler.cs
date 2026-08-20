using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>Serves polygon containment through the active spatial provider.</summary>
public sealed class SearchWithinPolygonQueryHandler
    : IQueryHandler<SearchWithinPolygonQuery, IReadOnlyList<AreaSummaryResponse>>
{
    private readonly GeospatialProviderRegistry _spatial;
    private readonly IAdministrativeAreaRepository _areas;

    public SearchWithinPolygonQueryHandler(
        GeospatialProviderRegistry spatial, IAdministrativeAreaRepository areas)
    {
        _spatial = spatial;
        _areas = areas;
    }

    public async Task<Result<IReadOnlyList<AreaSummaryResponse>>> Handle(
        SearchWithinPolygonQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_spatial.Supports(GeospatialCapability.PolygonSearch))
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(
                LocationErrors.SpatialCapabilityUnsupported(
                    "polygon search", _spatial.Active.Key));
        }

        var boundary = GeographicBoundary.TryCreate(request.Vertices);

        if (boundary.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(boundary.Error);
        }

        var countryCode = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : CountryCode.TryCreate(request.CountryCode) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;

        var search = PolygonSearch.Create(
            boundary.Value, request.Levels, countryCode, request.Limit);

        if (search.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(search.Error);
        }

        var found = await _spatial.Active.FindWithinPolygonAsync(search.Value, cancellationToken);

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "polygon"));

        var results = new List<AreaSummaryResponse>(found.Count);

        foreach (var result in found)
        {
            var ancestors = await _areas.GetAncestorsAsync(result.Area, cancellationToken);
            results.Add(LocationMapper.ToSummary(result.Area, ancestors, result.Distance));
        }

        return Result.Success<IReadOnlyList<AreaSummaryResponse>>(results);
    }
}
