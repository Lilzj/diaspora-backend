namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// What a search engine answered: hits in rank order, a cursor, and a total when the engine
/// knows one cheaply. Identifiers only — the caller hydrates cards from the write model.
/// </summary>
public sealed record GuideSearchOutcome
{
    /// <summary>The page, best first.</summary>
    public IReadOnlyList<GuideSearchHit> Hits { get; init; } = [];

    /// <summary>Opaque, engine-owned. Null on the last page.</summary>
    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>Total matches, when the engine knows. Null rather than a guess.</summary>
    public long? Total { get; init; }

    public static GuideSearchOutcome Empty { get; } = new();
}
