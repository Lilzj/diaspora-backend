namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// An area's outline, as a closed ring of coordinates plus its extent.
/// <para>
/// <b>The geometry has been stored since B4 and was never published.</b> <c>hasBoundary</c> told a
/// client an outline existed and gave it no way to draw one, so every map on the platform drew a
/// pin where a shape belonged. This is that shape.
/// </para>
/// <para>
/// The ring is <b>not closed</b>: the last vertex implicitly joins the first, which is how the
/// domain stores it. A renderer that needs a closed ring repeats the first point; emitting it
/// twice here would make a client that does not de-duplicate draw a zero-length final segment,
/// and would put a decision the domain has already made into every consumer.
/// </para>
/// <para>
/// <b>A boundary is a claim about where a place ends, and members rent on that line.</b>
/// <see cref="SimplifiedTo"/> exists so a client can say it is drawing a simplification rather
/// than presenting an imported ring as surveyed truth, and the provenance fields say whose claim
/// it is. Until 17 Sep 2026 there was no <c>source</c> here, because the catalogue recorded none;
/// it now stores one with every outline and will not store an outline without it.
/// </para>
/// <para>
/// <b>A client that draws the ring shows <see cref="Attribution"/> with it.</b> The outlines the
/// catalogue imports are OpenStreetMap data under the Open Database License, whose terms require
/// that credit wherever the data is displayed.
/// </para>
/// </summary>
public sealed record GeographicBoundaryResponse
{
    /// <summary>The ring, in order. At least three points.</summary>
    public required IReadOnlyList<GeographicVertexResponse> Vertices { get; init; }

    /// <summary>The ring's own extent. Always consistent with <see cref="Vertices"/>.</summary>
    public required BoundingBoxResponse Extent { get; init; }

    /// <summary>
    /// The number of points the ring carries. A caller deciding whether to render an outline or a
    /// pin needs to know it is looking at a simplification, and the count is the plainest signal.
    /// </summary>
    public required int SimplifiedTo { get; init; }

    /// <summary>
    /// Who published the outline: "OpenStreetMap". Null only for an outline stored before sources
    /// were recorded, which no deployment holds.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>The licence the outline is under, as an SPDX identifier: "ODbL-1.0".</summary>
    public string? Licence { get; init; }

    /// <summary>The credit the licence requires, verbatim, to show beside the drawn outline.</summary>
    public string? Attribution { get; init; }

    /// <summary>The source's own identifier for the shape, e.g. "relation/175342".</summary>
    public string? SourceReference { get; init; }

    /// <summary>The day the shape was taken from the source.</summary>
    public DateOnly? RetrievedOn { get; init; }
}

/// <summary>One point on an outline.</summary>
public sealed record GeographicVertexResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}
