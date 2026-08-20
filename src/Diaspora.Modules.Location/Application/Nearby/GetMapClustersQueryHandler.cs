using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Aggregates a viewport into markers a map can render at any zoom.
/// <para>
/// <b>The cells are catalogue nodes, not grid tiles, and that is the whole design.</b> Everything
/// this needs already exists: the spatial provider answers "which nodes are in this rectangle",
/// and <see cref="ILocationResourceCounter"/> has answered "how much of your vertical is in this
/// node" since B4. A grid would have required a second, coordinate-shaped seam in seven modules —
/// two of which (Jobs, Marketplace) deliberately store no coordinate at all and could never have
/// implemented it. Aggregating on places instead means no vertical changes, Jobs and Marketplace
/// are included rather than absent, and a cluster is something a member can tap through to.
/// </para>
/// <para>
/// <b>The aggregation itself lives in <see cref="MapCellAggregator"/>.</b> B21's density layer
/// needs the same cells, the same counters, the same cap and the same suppression floor in a
/// different response shape, and the platform's analyzer forbids the obvious alternative — one
/// handler sending another's query. This handler is now the mapping from those cells to the
/// cluster contract, which is all it should ever have been.
/// </para>
/// </summary>
public sealed class GetMapClustersQueryHandler
    : IQueryHandler<GetMapClustersQuery, MapClusterPageResponse>
{
    /// <summary>
    /// Kept as a public constant because it is part of what this route promises a client, and a
    /// test asserts against it. The implementation moved; the promise did not.
    /// </summary>
    public const int MaxCells = MapCellAggregator.MaxCells;

    private readonly MapCellAggregator _cells;

    public GetMapClustersQueryHandler(MapCellAggregator cells) => _cells = cells;

    public async Task<Result<MapClusterPageResponse>> Handle(
        GetMapClustersQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _cells.AggregateAsync(
            request.Bounds,
            request.Level,
            request.ResourceTypes,
            request.CountryCode,
            cancellationToken);

        if (page.IsFailure)
        {
            return Result.Failure<MapClusterPageResponse>(page.Error);
        }

        LocationTelemetry.SpatialQueries.Add(
            1, new KeyValuePair<string, object?>("kind", "clusters"));

        return Result.Success(new MapClusterPageResponse
        {
            Clusters =
            [
                .. page.Value.Cells.Select(cell => new MapClusterResponse
                {
                    AreaId = cell.Area.Id.Value,
                    Name = cell.Area.Name.Value,
                    Slug = cell.Area.Slug.Value,
                    Level = cell.Area.Level.ToString(),
                    Latitude = cell.Area.Center!.Latitude,
                    Longitude = cell.Area.Center!.Longitude,
                    Counts = cell.Counts,
                    Total = cell.Total
                })
            ],
            Bounds = LocationMapper.ToBounds(page.Value.Bounds)!,
            Level = page.Value.Level.ToString(),
            ResourceTypes = page.Value.ResourceTypes,
            MinimumCellCount = page.Value.MinimumCellCount,
            SuppressedCells = page.Value.SuppressedCells,
            Truncated = page.Value.Truncated
        });
    }
}
