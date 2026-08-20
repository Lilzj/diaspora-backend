namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// One page of search results, plus what it took to produce them.
/// <para>
/// <see cref="Engine"/> is on the response because the answer to "why did search behave
/// differently in production" is almost always "a different engine is registered there", and a
/// support conversation that starts with that fact is a much shorter one. It is also what the
/// capabilities endpoint reports.
/// </para>
/// <para>
/// <see cref="TotalCount"/> is nullable because an external index can usually give one cheaply and
/// a cursor-paged relational query cannot. A caller must render "about N results" only when it is
/// present, rather than counting the page.
/// </para>
/// </summary>
public sealed record KnowledgeSearchOutcome
{
    public IReadOnlyList<KnowledgeSearchHit> Hits { get; init; } = [];

    /// <summary>The opaque continuation token, or null at the end of the results.</summary>
    public string? NextCursor { get; init; }

    public bool HasMore { get; init; }

    /// <summary>How many results exist in total, when the engine can say cheaply.</summary>
    public int? TotalCount { get; init; }

    /// <summary>The engine that answered. Diagnostic, and reported by the capabilities endpoint.</summary>
    public required string Engine { get; init; }

    public static KnowledgeSearchOutcome Empty(string engine) => new() { Engine = engine };
}
