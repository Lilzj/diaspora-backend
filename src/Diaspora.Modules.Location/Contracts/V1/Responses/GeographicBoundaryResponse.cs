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
/// <b>A boundary is a claim about where a neighbourhood ends, and members rent on that line.</b>
/// <see cref="SimplifiedTo"/> exists so a client can say it is drawing a simplification rather
/// than presenting an imported ring as surveyed truth. There is deliberately no <c>source</c>
/// field: the catalogue records no provenance for an outline today, and a field that is always
/// null is a worse answer than an absent one — it reads as "unknown source" when the truth is
/// "nobody has been asked to record one".
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
}

/// <summary>One point on an outline.</summary>
public sealed record GeographicVertexResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}
