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
    /// The catalogue node the cell aggregates.
    /// <para>
    /// Present because these cells are places rather than grid tiles, so a member can tap one and
    /// go somewhere. It is also what makes the layer honest about its own resolution: a client can
    /// see that the "cell" is Peckham, not a 500-metre square.
    /// </para>
    /// </summary>
    public required string AreaName { get; init; }

    public required Guid AreaId { get; init; }
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
/// every point is a catalogue node's centroid with a count attached, which is what §29 means by
/// "area plus intensity, not individual users".
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

    /// <summary>The rung the cells sit on: <c>Neighborhood</c> or <c>City</c>.</summary>
    public required string Level { get; init; }

    /// <summary>The floor a cell must reach to be returned.</summary>
    public required int MinimumCellCount { get; init; }

    /// <summary>How many cells were withheld by that floor. Published so suppression is visible.</summary>
    public required int SuppressedCells { get; init; }

    /// <summary>Whether the viewport held more nodes than one response aggregates.</summary>
    public required bool Truncated { get; init; }

    /// <summary>The area covered, so a UI never implies global coverage.</summary>
    public required BoundsResponse Bounds { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}
