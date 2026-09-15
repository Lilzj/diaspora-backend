namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One page of search results.</summary>
public sealed record HousingSearchResultResponse
{
    public IReadOnlyList<HousingListingCardResponse> Items { get; init; } = [];

    /// <summary>The opaque continuation token. Null when there is no next page.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>Null rather than zero when the engine does not count. Zero means no results.</summary>
    public int? TotalCount { get; init; }

    public HousingSearchFacetsResponse? Facets { get; init; }

    /// <summary>
    /// A human-readable name for whatever geography was resolved, so a results page can say
    /// "near Peckham" rather than echoing what the member typed.
    /// </summary>
    public string? ResolvedLocation { get; init; }

    /// <summary>
    /// Which engine answered. Surfaced so an operator can tell whether the external engine or the
    /// EF fallback served a request without reading a log.
    /// </summary>
    public string? Engine { get; init; }

    /// <summary>
    /// The order the page is actually in: the one asked for, or the one used instead. A client that
    /// asked for <c>Nearest</c> and reads <c>Newest</c> here can say why the list is not by distance
    /// rather than implying that it is.
    /// </summary>
    public required string AppliedSort { get; init; }
}
