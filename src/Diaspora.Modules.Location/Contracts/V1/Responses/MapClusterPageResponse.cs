namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// Aggregated markers for a map viewport.
/// <para>
/// <b>Why the server aggregates at all.</b> A client can cluster five thousand pins and cannot
/// cluster the ones it never fetched — beyond a city's worth of content the shape is wrong, not
/// slow. Aggregating here means a dense viewport costs one response whose size depends on how many
/// <em>places</em> are in view rather than on how much is in them.
/// </para>
/// </summary>
public sealed record MapClusterPageResponse
{
    /// <summary>The cells, largest first.</summary>
    public required IReadOnlyList<MapClusterResponse> Clusters { get; init; }

    /// <summary>The viewport as the server read it, so a client can confirm what it asked.</summary>
    public required BoundingBoxResponse Bounds { get; init; }

    /// <summary>
    /// The granularity asked for — <c>Neighborhood</c> or <c>City</c>. Since 14 Oct 2026 it chooses
    /// the grid's cell size; the cells themselves are squares, not places.
    /// </summary>
    public required string Level { get; init; }

    /// <summary>The resource types actually counted, after any filter the caller applied.</summary>
    public required IReadOnlyList<string> ResourceTypes { get; init; }

    /// <summary>
    /// The floor a cell's total must reach to be returned.
    /// <para>
    /// Stated rather than assumed. Today the counters aggregate <b>published, individually
    /// discoverable content</b> — every listing behind a count can already be fetched one by one
    /// from its own module's search route — so the floor is 1 and a cluster of one job is not a
    /// disclosure. The moment a counter aggregates <em>people</em> that stops being true, and
    /// <c>Location:Spatial:MinimumClusterCellCount</c> is the knob that raises the floor before
    /// such a counter ships rather than after somebody notices.
    /// </para>
    /// </summary>
    public required int MinimumCellCount { get; init; }

    /// <summary>
    /// How many cells were dropped for falling under <see cref="MinimumCellCount"/>. Published so
    /// that suppression is visible rather than indistinguishable from an empty map.
    /// </summary>
    public required int SuppressedCells { get; init; }

    /// <summary>
    /// Whether a vertical had more rows in the viewport than it reads for one request, so the
    /// totals are lower bounds.
    /// <para>
    /// True means "zoom in for exact figures", and it is never silent: a truncated map that says
    /// nothing reads as a complete one.
    /// </para>
    /// </summary>
    public required bool Truncated { get; init; }

    /// <summary>
    /// Added 14 Oct 2026: <c>grid</c>. The cells are squares over each row's own coordinates; a
    /// row with no coordinates is not on the map.
    /// </summary>
    public string Aggregation { get; init; } = "grid";

    /// <summary>Added 14 Oct 2026. The side of every cell, in degrees of latitude and longitude.</summary>
    public double? CellSizeDegrees { get; init; }
}
