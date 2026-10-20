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

    /// <summary>
    /// Added 21 Oct 2026. How many rows the cell's value was computed from: the homes behind a rent
    /// median, the reports or crimes behind a safety count. Equal to <see cref="RawValue"/> on a
    /// count layer.
    /// </summary>
    public int? SampleSize { get; init; }

    /// <summary>
    /// Added 21 Oct 2026. A safety cell's count per the source's own categories (community
    /// <c>Police</c> / <c>Emergency</c>, or data.police.uk's category ids). Null on every other layer.
    /// </summary>
    public IReadOnlyDictionary<string, int>? Categories { get; init; }
}

/// <summary>
/// A cell that holds something and was not drawn, with why (21 Oct 2026).
/// <para>
/// <b>Withheld is not zero.</b> A cell under its layer's minimum sample is left off the map and
/// listed here — its place on the grid and a reason, never its count or value, because publishing
/// the figure would defeat the minimum.
/// </para>
/// </summary>
public sealed record WithheldHeatCellResponse
{
    public required string CellKey { get; init; }

    public required Guid AreaId { get; init; }

    public required string AreaName { get; init; }

    public required BoundsResponse CellBounds { get; init; }

    /// <summary>
    /// <c>BelowMinimumCount</c> (fewer rows than the map's floor), <c>TooFewHomes</c> (a rent median
    /// needs more homes), or <c>CountrySampleTooSmall</c> (an index needs a baseline the country
    /// does not have yet).
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>The reason as a sentence, naming the minimum.</summary>
    public required string Detail { get; init; }
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

    /// <summary>
    /// Added 21 Oct 2026. Which source the values come from: <c>platform-content</c>,
    /// <c>listings</c>, <c>community-reports</c> or <c>police-recorded-crime</c>. A safety layer is
    /// always one source, never a blend.
    /// </summary>
    public string Source { get; init; } = "platform-content";

    /// <summary>
    /// Added 21 Oct 2026. False when the source could not answer for this viewport at all — not
    /// read in this deployment, not published for the country, not yet published for the time
    /// range, or a cost-of-living baseline the country does not have. The points are then empty and
    /// mean nothing; <see cref="UnavailableReason"/> says which.
    /// </summary>
    public bool IsAvailable { get; init; } = true;

    public string? UnavailableReason { get; init; }

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

    /// <summary>Added 21 Oct 2026. Which cells were withheld, and why. Never their values.</summary>
    public IReadOnlyList<WithheldHeatCellResponse> WithheldCells { get; init; } = [];

    /// <summary>
    /// Added 21 Oct 2026. The fewest homes a country needs before the cost-of-living index is computed
    /// against it. Null on every other layer.
    /// </summary>
    public int? MinimumCountrySample { get; init; }

    /// <summary>Added 21 Oct 2026. The currency of a rent layer's values. Null on every other layer.</summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Added 21 Oct 2026. The time range asked for — <c>24h</c>, <c>7d</c>, <c>30d</c>, <c>90d</c> —
    /// or null when none was.
    /// </summary>
    public string? TimeRange { get; init; }

    /// <summary>
    /// Added 21 Oct 2026. Which time the range filters on: <c>published</c>, <c>starts</c> (an event:
    /// starting before the range ends, those under way included) or <c>month</c> (police data, by
    /// whole published months).
    /// </summary>
    public string? TimeBasis { get; init; }

    /// <summary>Added 21 Oct 2026. The period the values cover, where the layer has one.</summary>
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Added 21 Oct 2026. The licence the source's data is republished under, when it has one.</summary>
    public string? Licence { get; init; }

    /// <summary>Added 21 Oct 2026. The credit the source requires wherever its data is shown.</summary>
    public string? Attribution { get; init; }

    /// <summary>Whether a vertical had more rows in the viewport than one request reads.</summary>
    public required bool Truncated { get; init; }

    /// <summary>The area covered, so a UI never implies global coverage.</summary>
    public required BoundsResponse Bounds { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}
