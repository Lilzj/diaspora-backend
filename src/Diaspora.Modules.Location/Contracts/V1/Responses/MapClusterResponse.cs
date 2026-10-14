namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// One aggregated marker on a map: a square grid cell and how much of each vertical sits in it.
/// <para>
/// <b>A grid cell, not a place, since 14 Oct 2026.</b> The platform is retiring its own place
/// catalogue, and these cells are now squares on a world grid over each row's own coordinates, so
/// a country the catalogue never held is on the map like any other. The fields a place used to fill
/// are kept so existing clients keep working: <see cref="AreaId"/> is the cell's stable identifier,
/// <see cref="Name"/> a plain description of where it is, <see cref="Slug"/> its
/// <see cref="CellKey"/>, and <see cref="Level"/> is <c>Grid</c>. Tapping a cell zooms into
/// <see cref="Bounds"/>; there is no page behind it.
/// </para>
/// </summary>
public sealed record MapClusterResponse
{
    /// <summary>The cell's stable identifier — the same cell has the same id on every request.</summary>
    public required Guid AreaId { get; init; }

    /// <summary>"Area around 4.05°S, 39.67°E" — where the cell is, for an accessible label.</summary>
    public required string Name { get; init; }

    /// <summary>The <see cref="CellKey"/>.</summary>
    public required string Slug { get; init; }

    /// <summary><c>Grid</c>.</summary>
    public required string Level { get; init; }

    /// <summary>
    /// Where the marker goes: the mean of the rows in the cell when it holds three or more,
    /// otherwise the cell's centre, so a marker never sits on one row's pin.
    /// </summary>
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }

    /// <summary>
    /// Counts by resource type.
    /// <para>
    /// An absent key means the vertical could not be asked. A zero means it was asked and has
    /// nothing in this cell. A client that renders the two the same tells a member there is no
    /// housing somewhere when the truth is that Housing did not answer.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, int> Counts { get; init; }

    /// <summary>The sum across the returned types. What a single-number marker renders.</summary>
    public required int Total { get; init; }

    /// <summary>
    /// Added 14 Oct 2026. <c>{exponent}:{row}:{column}</c> on a world grid anchored at 0°,0° whose
    /// cell side is 2^exponent degrees.
    /// </summary>
    public string? CellKey { get; init; }

    /// <summary>Added 14 Oct 2026. The cell's edges — what a tap should zoom to.</summary>
    public BoundingBoxResponse? Bounds { get; init; }
}
