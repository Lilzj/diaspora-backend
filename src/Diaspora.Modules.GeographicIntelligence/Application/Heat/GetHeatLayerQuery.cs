using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;
using Diaspora.Modules.GeographicIntelligence.Domain.Errors;
using Diaspora.Modules.GeographicIntelligence.Domain.Heat;
using Diaspora.Modules.GeographicIntelligence.Observability;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.GeographicIntelligence.Application.Heat;

/// <summary>A density layer for a viewport.</summary>
public sealed record GetHeatLayerQuery : IQuery<HeatLayerResponse>
{
    /// <summary>The client's layer id: <c>housing-availability</c>, <c>jobs</c>, …</summary>
    public required string Layer { get; init; }

    /// <summary>The viewport: <c>north,south,east,west</c>. Parsed and validated by B4.</summary>
    public required string Bounds { get; init; }

    /// <summary>
    /// The rung to aggregate on, named outright. Takes precedence over <see cref="Zoom"/>.
    /// <para>
    /// Deriving the rung from the box's <em>span</em> would be the server guessing at a viewport
    /// whose pixels it cannot see, and would change the answer under a client that resized rather
    /// than zoomed. Deriving it from a zoom the client <em>stated</em> is a different thing, and is
    /// what <see cref="Zoom"/> is for.
    /// </para>
    /// </summary>
    public string? Level { get; init; }

    /// <summary>
    /// The client's map zoom, which the frontend's <c>fetchHeatLayer</c> already sends.
    /// <para>
    /// Honoured rather than ignored: the client knows its own zoom and saying so is not the server
    /// guessing. Below <see cref="CityZoomThreshold"/> a neighbourhood is smaller than a pixel and
    /// the honest rung is the city.
    /// </para>
    /// </summary>
    public double? Zoom { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>
    /// The zoom at or above which neighbourhood cells are legible.
    /// <para>
    /// Thirteen is roughly where a city fills a phone screen on the standard web-mercator scale.
    /// Below it a neighbourhood cluster is a pin on top of another pin.
    /// </para>
    /// </summary>
    public const double CityZoomThreshold = 12d;

    /// <summary>The rung this request implies. <see cref="Level"/> wins where both are given.</summary>
    public string? ResolvedLevel =>
        !string.IsNullOrWhiteSpace(Level)
            ? Level
            : Zoom is { } zoom
                ? zoom < CityZoomThreshold ? "City" : "Neighborhood"
                : null;
}

/// <summary>
/// Serves the density layer the frontend has been simulating since Phase 8.
/// <para>
/// <b>Composed from B4's cells, not from a second aggregation.</b> Wave 4 shipped
/// <c>GET /location/map/clusters</c> and recorded the heat-map gap as closed by it. The capability
/// was right and the contract was not: the client's <c>fetchHeatLayer</c> asks for a layer, a
/// normalised intensity, a min and a max, a scale statement and provenance, and a cluster page
/// carries none of those — which is why the client is still calling its development adapter for
/// this. B21 does not re-aggregate anything; it takes the same
/// <see cref="MapCellAggregator"/> cells and answers in the shape the client declared.
/// </para>
/// <para>
/// <b>Three privacy properties, and none of them is new.</b> Every point is a square grid cell's
/// centre or the mean of three or more rows in it (14 Oct 2026; catalogue centroids before),
/// never an address. Every value is a count of individually discoverable published
/// content, never of people. And the minimum-cell-count floor is B4's own — the same
/// <c>Location:Spatial:MinimumClusterCellCount</c> knob, published on the response as
/// <c>minimumCellCount</c> with the number of cells it withheld, so suppression is visible rather
/// than indistinguishable from an empty map.
/// </para>
/// <para>
/// <b>An unsupported layer is refused rather than answered empty.</b> An empty heat layer renders
/// as a quiet neighbourhood, which is a claim about the world; a named error is a claim about the
/// platform, and it is the true one.
/// </para>
/// </summary>
public sealed class GetHeatLayerQueryHandler : IQueryHandler<GetHeatLayerQuery, HeatLayerResponse>
{
    private const string PlatformSource = "Diaspora platform content";

    private readonly MapCellAggregator _cells;
    private readonly IOptions<GeographicIntelligenceOptions> _options;
    private readonly TimeProvider _clock;

    public GetHeatLayerQueryHandler(
        MapCellAggregator cells,
        IOptions<GeographicIntelligenceOptions> options,
        TimeProvider clock)
    {
        _cells = cells;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<HeatLayerResponse>> Handle(
        GetHeatLayerQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeographicIntelligenceTelemetry.Requests.Add(
            1, new KeyValuePair<string, object?>("surface", "heat"));

        var definition = HeatLayerCatalog.Find(request.Layer);

        if (definition is null)
        {
            return Result.Failure<HeatLayerResponse>(
                GeographicIntelligenceErrors.LayerUnsupported(
                    request.Layer ?? string.Empty,
                    string.Join(", ", HeatLayerCatalog.SupportedLayers)));
        }

        var span = SpanOf(request.Bounds);

        if (span is null)
        {
            return Result.Failure<HeatLayerResponse>(GeographicIntelligenceErrors.BoundsInvalid);
        }

        var maximum = _options.Value.MaximumHeatSpanDegrees;

        if (span > maximum)
        {
            GeographicIntelligenceTelemetry.BoundsRejected.Add(
                1, new KeyValuePair<string, object?>("surface", "heat"));

            return Result.Failure<HeatLayerResponse>(
                GeographicIntelligenceErrors.BoundsTooLarge(span.Value, maximum));
        }

        // One resource type, so a single-layer map over twenty cells is twenty counts rather than
        // a hundred and forty. Narrowing here is what makes the layer affordable at all.
        var page = await _cells.AggregateAsync(
            request.Bounds,
            request.ResolvedLevel,
            [definition.ResourceType],
            request.CountryCode,
            cellSizeKm: null,
            cancellationToken);

        if (page.IsFailure)
        {
            GeographicIntelligenceTelemetry.Errors.Add(
                1, new KeyValuePair<string, object?>("code", page.Error.Code));

            return Result.Failure<HeatLayerResponse>(page.Error);
        }

        var cells = page.Value.Cells;

        GeographicIntelligenceTelemetry.CellsSuppressed.Add(
            page.Value.SuppressedCells, new KeyValuePair<string, object?>("layer", definition.Layer));

        var values = cells
            .Select(cell => cell.Counts.TryGetValue(definition.ResourceType, out var count) ? count : 0)
            .ToList();

        var minimum = values.Count == 0 ? 0 : values.Min();
        var peak = values.Count == 0 ? 0 : values.Max();

        var now = _clock.GetUtcNow();

        return Result.Success(new HeatLayerResponse
        {
            Layer = definition.Layer,
            Unit = definition.Unit,
            Points =
            [
                .. cells.Select((cell, index) => new HeatPointResponse
                {
                    Latitude = cell.Latitude,
                    Longitude = cell.Longitude,
                    Intensity = Normalize(values[index], minimum, peak),
                    RawValue = values[index],
                    AreaName = cell.Label,
                    AreaId = cell.Id,
                    CellKey = cell.Key,
                    CellBounds = new BoundsResponse
                    {
                        North = cell.North,
                        South = cell.South,
                        East = cell.East,
                        West = cell.West
                    }
                })
            ],

            // Always fitted. An absolute scale needs a platform-wide maximum, and every candidate
            // for that number is a constant nobody measured.
            Scale = "relative-to-viewport",
            MinValue = minimum,
            MaxValue = peak,
            Level = page.Value.Level.ToString(),
            CellSizeDegrees = page.Value.CellSizeDegrees,
            MinimumCellCount = page.Value.MinimumCellCount,
            SuppressedCells = page.Value.SuppressedCells,
            Truncated = page.Value.Truncated,
            Bounds = new BoundsResponse
            {
                North = page.Value.Bounds.North,
                South = page.Value.Bounds.South,
                East = page.Value.Bounds.East,
                West = page.Value.Bounds.West
            },
            Freshness = new DataFreshnessResponse
            {
                ComputedAt = now,
                SourceName = PlatformSource,
                Methodology =
                    $"Live count of published {definition.Unit} per square grid cell inside the "
                    + "viewport, from each row's own coordinates, computed on request. Intensity is "
                    + "scaled to the highest cell in this viewport, so it is comparable within this "
                    + "layer and not across layers or across viewports.",
                Limitations =
                [
                    $"Counts {definition.Unit} published on Diaspora, not what exists in the area.",
                    "Each point is a grid cell's centre, or the mean of three or more rows in it, "
                    + "never an address. No individual record is identifiable from this layer.",
                    "Rows without coordinates are not counted.",
                    $"Cells with fewer than {page.Value.MinimumCellCount} are withheld; "
                    + $"{page.Value.SuppressedCells} were withheld here."
                ],
                ValidUntil = now.AddHours(_options.Value.FreshnessWindowHours),
                SampleSize = cells.Count
            }
        });
    }

    /// <summary>
    /// Latitude span of the requested box, or null when it does not parse.
    /// <para>
    /// Latitude only, deliberately. Longitude span is ambiguous across the antimeridian — a box
    /// from 179°E to 179°W is two degrees wide, and subtracting says it is 358 — so a longitude
    /// check here would refuse the one viewport B4 went out of its way to support.
    /// </para>
    /// </summary>
    private static double? SpanOf(string? bounds)
    {
        var parts = bounds?.Split(',');

        if (parts is not { Length: 4 })
        {
            return null;
        }

        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var north)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var south))
        {
            return null;
        }

        return double.IsFinite(north) && double.IsFinite(south) ? Math.Abs(north - south) : null;
    }

    /// <summary>
    /// Scales a count onto 0–1 against this viewport's own range.
    /// <para>
    /// When every cell holds the same number there is no range to scale against, and the honest
    /// answer is that they are all equally busy — 1, not 0. Returning 0 would draw a uniformly
    /// active district as empty.
    /// </para>
    /// </summary>
    private static double Normalize(int value, int minimum, int peak) =>
        peak <= minimum ? 1d : Math.Round((value - minimum) / (double)(peak - minimum), 4);
}
