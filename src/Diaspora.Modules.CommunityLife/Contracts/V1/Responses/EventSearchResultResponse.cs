namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// A page of events.
/// <para>
/// <b><see cref="Engine"/> names which implementation answered.</b> A deployment can swap the
/// shipped database search for something else, and a client that can see which one ran can tell a
/// relevance-ordered page from a date-ordered one without guessing from the results.
/// </para>
/// </summary>
public sealed record EventSearchResultResponse
{
    public IReadOnlyList<EventSummaryResponse> Items { get; init; } = [];

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public int? TotalCount { get; init; }

    /// <summary>Counts per facet value, for a filter bar that shows how much each option holds.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Facets { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);

    public string? Engine { get; init; }
}
