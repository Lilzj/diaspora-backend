namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A page of search results.
/// <para>
/// <see cref="TotalMatches"/> is nullable because an honest engine that cannot count cheaply says
/// so. "About 400 results" rendered from a number the engine guessed is worse than no number.
/// </para>
/// </summary>
public sealed record ListingSearchResultResponse
{
    public required IReadOnlyList<ListingCardResponse> Items { get; init; }

    /// <summary>The continuation. Opaque, and null on the last page.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>Total matches, when the engine can say cheaply.</summary>
    public long? TotalMatches { get; init; }

    /// <summary>The counts beside the filters. Null when the caller did not ask for them.</summary>
    public ListingSearchFacetsResponse? Facets { get; init; }

    /// <summary>
    /// Which geography the results were actually scoped to, as Location resolved it. Present so a
    /// client can render "near Peckham" rather than echoing what the user typed.
    /// </summary>
    public string? ResolvedScope { get; init; }

    /// <summary>The ranking strategy that ordered them. For the "sorted by" line.</summary>
    public string? Ranking { get; init; }
}
