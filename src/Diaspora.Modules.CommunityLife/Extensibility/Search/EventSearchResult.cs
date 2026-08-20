namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>
/// What a search engine answers with.
/// <para>
/// The cursor is opaque to the module and belongs to the engine: the EF engine encodes a sort key
/// and a tie-break, and an OpenSearch adapter would encode a search-after token. Nothing above this
/// type reads inside it.
/// </para>
/// </summary>
public sealed record EventSearchResult
{
    public IReadOnlyList<EventSearchHit> Hits { get; init; } = [];

    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>
    /// The total, when the engine can supply one cheaply. Null rather than zero when it cannot —
    /// an exact count over a large index is the query that makes a search slow, and a nullable field
    /// lets an engine decline it honestly.
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>Facet counts, when the engine computes them. Empty when it does not.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>();

    public static EventSearchResult Empty { get; } = new();
}
