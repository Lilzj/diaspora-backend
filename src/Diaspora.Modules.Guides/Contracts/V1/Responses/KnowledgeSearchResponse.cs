namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One page of knowledge search results.
/// <para>
/// <see cref="Engine"/> is on the response deliberately. The answer to "why does search behave
/// differently in production" is almost always "a different engine is registered there", and a
/// support conversation that begins with that fact is a much shorter one. It is diagnostic
/// information, not a secret.
/// </para>
/// <para>
/// <see cref="TotalCount"/> is nullable because an external index can usually produce one cheaply
/// and a cursor-paged relational query cannot. A client renders "about N results" only when it is
/// present, and never counts the page it happens to be holding.
/// </para>
/// </summary>
public sealed record KnowledgeSearchResponse
{
    public IReadOnlyList<GuideCardResponse> Results { get; init; } = [];

    /// <summary>The opaque continuation token, or null at the end.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>How many results exist in total, when the engine can say cheaply.</summary>
    public int? TotalCount { get; init; }

    /// <summary>Which engine answered.</summary>
    public required string Engine { get; init; }

    /// <summary>
    /// The country the search was scoped to, echoed back.
    /// <para>
    /// Echoed because a client that sent no country and a client whose country was rejected as
    /// malformed would otherwise be unable to tell their results apart from a global search.
    /// </para>
    /// </summary>
    public string? CountryCode { get; init; }
}
