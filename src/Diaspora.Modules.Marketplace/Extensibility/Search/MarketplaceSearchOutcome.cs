namespace Diaspora.Modules.Marketplace.Extensibility.Search;

/// <summary>
/// What an engine answers with.
/// <para>
/// <see cref="TotalHits"/> is nullable because an honest engine that cannot count cheaply should
/// say so. A result page that renders "about 400 results" from a number the engine guessed is
/// worse than one that renders no number at all.
/// </para>
/// </summary>
public sealed record MarketplaceSearchOutcome
{
    public required IReadOnlyList<MarketplaceSearchHit> Hits { get; init; }

    /// <summary>The continuation, opaque and engine-owned. Null on the last page.</summary>
    public string? NextCursor { get; init; }

    /// <summary>Total matches, when the engine can say cheaply. Null when it cannot.</summary>
    public long? TotalHits { get; init; }

    /// <summary>Nothing found, and no continuation. The answer an unavailable engine may give.</summary>
    public static MarketplaceSearchOutcome Empty => new() { Hits = [] };
}
