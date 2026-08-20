namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>
/// Asks what catalogue nodes fall inside a drawn outline.
/// <para>
/// A POST rather than a GET because a polygon does not fit in a query string — a simplified
/// city outline is several hundred coordinate pairs, well past what proxies and browsers
/// accept in a URL.
/// </para>
/// </summary>
public sealed record PolygonSearchRequest
{
    /// <summary>
    /// The ring, in order. Closing the ring by repeating the first point is accepted and
    /// normalised away, so both the GeoJSON convention and the open form work.
    /// </summary>
    public required IReadOnlyList<PolygonVertexRequest> Vertices { get; init; }

    /// <summary>Restricts results to these levels. Empty means every level.</summary>
    public IReadOnlyList<string>? Levels { get; init; }

    public string? CountryCode { get; init; }

    public int? Limit { get; init; }
}
