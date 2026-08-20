namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// One search result: an identifier and, when the engine scores, a score. The EF engine
/// returns no scores — filter order is not relevance, and a fabricated number would be worse
/// than none.
/// </summary>
public sealed record GuideSearchHit
{
    public required Guid GuideId { get; init; }

    /// <summary>The engine's relevance score, when it computes one. Comparable within one outcome only.</summary>
    public double? Score { get; init; }
}
