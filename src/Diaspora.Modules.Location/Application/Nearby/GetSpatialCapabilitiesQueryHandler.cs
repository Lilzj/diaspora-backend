using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>Reports the active spatial provider and the registered contributions.</summary>
public sealed class GetSpatialCapabilitiesQueryHandler
    : IQueryHandler<GetSpatialCapabilitiesQuery, SpatialCapabilitiesResponse>
{
    private readonly GeospatialProviderRegistry _spatial;
    private readonly NearbyResourceProviderRegistry _nearby;
    private readonly LocationResourceCounterRegistry _counters;
    private readonly IGeoDistanceCalculator _calculator;

    public GetSpatialCapabilitiesQueryHandler(
        GeospatialProviderRegistry spatial,
        NearbyResourceProviderRegistry nearby,
        LocationResourceCounterRegistry counters,
        IGeoDistanceCalculator calculator)
    {
        _spatial = spatial;
        _nearby = nearby;
        _counters = counters;
        _calculator = calculator;
    }

    public Task<Result<SpatialCapabilitiesResponse>> Handle(
        GetSpatialCapabilitiesQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new SpatialCapabilitiesResponse
        {
            Provider = _spatial.Active.Key,
            AvailableProviders = _spatial.AvailableKeys,
            DistanceCalculator = _calculator.Name,
            SupportsRadiusSearch = _spatial.Supports(GeospatialCapability.RadiusSearch),
            SupportsBoundingBoxSearch = _spatial.Supports(GeospatialCapability.BoundingBoxSearch),
            SupportsPolygonSearch = _spatial.Supports(GeospatialCapability.PolygonSearch),
            SupportsNearestNeighbor = _spatial.Supports(GeospatialCapability.NearestNeighbor),
            NearbyResourceTypes = _nearby.ResourceTypes,
            CountedResourceTypes = _counters.All.Select(counter => counter.ResourceType).ToList()
        }));
}
