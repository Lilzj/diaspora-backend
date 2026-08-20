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

/// <summary>Serves viewport queries through the active spatial provider.</summary>
public sealed class SearchWithinBoundsQueryHandler
    : IQueryHandler<SearchWithinBoundsQuery, IReadOnlyList<AreaSummaryResponse>>
{
    private readonly GeospatialProviderRegistry _spatial;
    private readonly IAdministrativeAreaRepository _areas;

    public SearchWithinBoundsQueryHandler(
        GeospatialProviderRegistry spatial, IAdministrativeAreaRepository areas)
    {
        _spatial = spatial;
        _areas = areas;
    }

    public async Task<Result<IReadOnlyList<AreaSummaryResponse>>> Handle(
        SearchWithinBoundsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Asked before the query runs, so an unsupported capability fails with a reason rather
        // than returning an empty list a caller would read as "nothing here".
        if (!_spatial.Supports(GeospatialCapability.BoundingBoxSearch))
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(
                LocationErrors.SpatialCapabilityUnsupported(
                    "bounding-box search", _spatial.Active.Key));
        }

        var bounds = BoundingBox.TryCreate(
            request.North, request.South, request.East, request.West);

        if (bounds.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(bounds.Error);
        }

        var countryCode = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : CountryCode.TryCreate(request.CountryCode) is { IsSuccess: true } parsed
                ? parsed.Value
                : null;

        var search = BoundingBoxSearch.Create(
            bounds.Value, request.Levels, countryCode, request.Limit);

        if (search.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AreaSummaryResponse>>(search.Error);
        }

        var found = await _spatial.Active.FindWithinBoundsAsync(search.Value, cancellationToken);

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "bounds"));

        var results = new List<AreaSummaryResponse>(found.Count);

        foreach (var result in found)
        {
            var ancestors = await _areas.GetAncestorsAsync(result.Area, cancellationToken);
            results.Add(LocationMapper.ToSummary(result.Area, ancestors, result.Distance));
        }

        return Result.Success<IReadOnlyList<AreaSummaryResponse>>(results);
    }
}
