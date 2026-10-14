using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Aggregates a viewport into markers a map can render at any zoom.
/// <para>
/// <b>Square grid cells over each row's own coordinates since 14 Oct 2026</b> (Phase 2B of retiring
/// the place catalogue). The aggregation lives in <see cref="MapCellAggregator"/>, shared with the
/// heat layer; this handler maps its cells onto the cluster contract, keeping every field the
/// catalogue-node cells filled so the app keeps working, and adding the cell's key and edges.
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

    /// <summary>What every cell's <c>level</c> says: it is a grid square, not a place.</summary>
    public const string GridLevel = "Grid";

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
            request.CellSizeKm,
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
                    AreaId = cell.Id,
                    Name = cell.Label,
                    Slug = cell.Key,
                    Level = GridLevel,
                    Latitude = cell.Latitude,
                    Longitude = cell.Longitude,
                    Counts = cell.Counts,
                    Total = cell.Total,
                    CellKey = cell.Key,
                    Bounds = new BoundingBoxResponse
                    {
                        North = cell.North,
                        South = cell.South,
                        East = cell.East,
                        West = cell.West
                    }
                })
            ],
            Bounds = LocationMapper.ToBounds(page.Value.Bounds)!,
            Level = page.Value.Level.ToString(),
            ResourceTypes = page.Value.ResourceTypes,
            MinimumCellCount = page.Value.MinimumCellCount,
            SuppressedCells = page.Value.SuppressedCells,
            Truncated = page.Value.Truncated,
            CellSizeDegrees = page.Value.CellSizeDegrees
        });
    }
}
