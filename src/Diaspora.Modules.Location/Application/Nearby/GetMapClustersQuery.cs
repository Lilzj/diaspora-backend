using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>Aggregated markers for a map viewport.</summary>
public sealed record GetMapClustersQuery : IQuery<MapClusterPageResponse>
{
    /// <summary>The viewport: <c>north,south,east,west</c>.</summary>
    public required string Bounds { get; init; }

    /// <summary>
    /// The granularity of the grid: <c>Neighborhood</c> (about 12 cells across, the default) or
    /// <c>City</c> (about 6). Since 14 Oct 2026 the cells are square grid cells over the rows' own
    /// coordinates, not catalogue nodes; the word only chooses how fine they are.
    /// </summary>
    public string? Level { get; init; }

    /// <summary>
    /// An explicit cell side in kilometres, overriding <see cref="Level"/>. Rounded up to the grid's
    /// power-of-two steps, and coarsened if the viewport would hold more than the cell cap.
    /// </summary>
    public double? CellSizeKm { get; init; }

    /// <summary>Which verticals to count. Empty means every counter this deployment has.</summary>
    public IReadOnlyList<string>? ResourceTypes { get; init; }

    public string? CountryCode { get; init; }
}
