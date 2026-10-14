namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>One cell of a density layer.</summary>
public sealed record HeatPointResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>0–1, comparable within this layer and never across layers.</summary>
    public required double Intensity { get; init; }

    /// <summary>The count before normalisation, for the legend and the accessible table.</summary>
    public required int RawValue { get; init; }

    /// <summary>
    /// A plain description of where the cell is: "Area around 4.05°S, 39.67°E".
    /// <para>
    /// Since 14 Oct 2026 the cells are square grid cells over each row's own coordinates, not
    /// catalogue places; the field keeps its name so existing clients keep working.
    /// </para>
    /// </summary>
    public required string AreaName { get; init; }

    /// <summary>The grid cell's stable identifier — the same cell has the same id on every request.</summary>
    public required Guid AreaId { get; init; }

    /// <summary>Added 14 Oct 2026. The cell's key on the world grid: <c>{exponent}:{row}:{column}</c>.</summary>
    public string? CellKey { get; init; }

    /// <summary>Added 14 Oct 2026. The cell's edges.</summary>
    public BoundsResponse? CellBounds { get; init; }
}

/// <summary>The rectangle a layer covers, echoed so a client can confirm what the server read.</summary>
public sealed record BoundsResponse
{
    public required double North { get; init; }

    public required double South { get; init; }

    public required double East { get; init; }

    public required double West { get; init; }
}

/// <summary>
/// A density layer for a viewport.
/// <para>
/// <b>Aggregates, never records.</b> No cell here is an entity and no coordinate is an address:
/// every point is a square grid cell's centre — or the mean of three or more rows in it — with a
/// count attached, which is what §29 means by "area plus intensity, not individual users".
/// </para>
/// <para>
/// <b>The suppression rule is B4's, not a second one.</b> The cells and their counts come from
/// <c>GetMapClustersQuery</c> — the same aggregation, the same minimum-cell-count floor, the same
/// <c>Location:Spatial:MinimumClusterCellCount</c> knob — because a density layer with its own
/// privacy threshold is a second threshold somebody has to remember to raise. What this module
/// adds is the normalisation, the layer vocabulary and the provenance.
/// </para>
/// </summary>
public sealed record HeatLayerResponse
{
    /// <summary>The client's layer id, echoed: <c>housing-availability</c>, <c>jobs</c>, …</summary>
    public required string Layer { get; init; }

    /// <summary>What the intensity counts. "listings", "events", "providers".</summary>
    public required string Unit { get; init; }

    public required IReadOnlyList<HeatPointResponse> Points { get; init; }

    /// <summary>
    /// <c>relative-to-viewport</c>. Stated rather than assumed.
    /// <para>
    /// A fitted scale makes a quiet region look busy, so the legend has to say which it is. This
    /// layer is always fitted, because an absolute scale needs a platform-wide maximum that would
    /// be a constant nobody measured.
    /// </para>
    /// </summary>
    public required string Scale { get; init; }

    /// <summary>The raw values the 0 and 1 ends of the gradient correspond to.</summary>
    public required int MinValue { get; init; }

    public required int MaxValue { get; init; }

    /// <summary>
    /// The granularity asked for: <c>Neighborhood</c> (finer) or <c>City</c> (coarser). Since
    /// 14 Oct 2026 it chooses the grid's cell size.
    /// </summary>
    public required string Level { get; init; }

    /// <summary>Added 14 Oct 2026. The side of every cell, in degrees.</summary>
    public double? CellSizeDegrees { get; init; }

    /// <summary>The floor a cell must reach to be returned.</summary>
    public required int MinimumCellCount { get; init; }

    /// <summary>How many cells were withheld by that floor. Published so suppression is visible.</summary>
    public required int SuppressedCells { get; init; }

    /// <summary>Whether a vertical had more rows in the viewport than one request reads.</summary>
    public required bool Truncated { get; init; }

    /// <summary>The area covered, so a UI never implies global coverage.</summary>
    public required BoundsResponse Bounds { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}
