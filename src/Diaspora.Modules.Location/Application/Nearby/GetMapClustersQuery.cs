using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>Aggregated markers for a map viewport.</summary>
public sealed record GetMapClustersQuery : IQuery<MapClusterPageResponse>
{
    /// <summary>The viewport: <c>north,south,east,west</c>.</summary>
    public required string Bounds { get; init; }

    /// <summary>
    /// The rung to aggregate on. <c>Neighborhood</c> by default, <c>City</c> when zoomed out.
    /// <para>
    /// The client chooses because the client knows its zoom. Deriving the rung from the box's
    /// span here would be the server guessing at a viewport it cannot see the pixels of, and
    /// would change the answer under a client that resized rather than zoomed.
    /// </para>
    /// </summary>
    public string? Level { get; init; }

    /// <summary>Which verticals to count. Empty means every counter this deployment has.</summary>
    public IReadOnlyList<string>? ResourceTypes { get; init; }

    public string? CountryCode { get; init; }
}
