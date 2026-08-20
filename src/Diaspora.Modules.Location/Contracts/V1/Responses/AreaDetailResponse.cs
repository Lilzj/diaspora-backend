namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>A hierarchy node in full, with its breadcrumb and whatever indexes exist.</summary>
public sealed record AreaDetailResponse
{
    public required AreaSummaryResponse Area { get; init; }

    /// <summary>Ancestors root-first, excluding the node itself.</summary>
    public required IReadOnlyList<AreaSummaryResponse> Ancestors { get; init; }

    public required CountrySummaryResponse Country { get; init; }

    public BoundingBoxResponse? Extent { get; init; }

    /// <summary>Whether an outline has been imported for this node.</summary>
    public required bool HasBoundary { get; init; }

    /// <summary>
    /// The outline itself, when one has been imported.
    /// <para>
    /// Omitted from the list projections deliberately: a simplified city ring is several hundred
    /// coordinate pairs, and a directory page that carried one per row would transfer more
    /// geometry than the screen could draw.
    /// </para>
    /// </summary>
    public GeographicBoundaryResponse? Boundary { get; init; }

    public string? PostalCode { get; init; }

    public string? Overview { get; init; }
}
