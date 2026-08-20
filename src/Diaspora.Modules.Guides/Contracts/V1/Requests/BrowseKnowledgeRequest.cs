namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// The filters every knowledge browse shelf shares, bound from the query string.
/// <para>
/// Used by the shelves that are their own endpoints — featured, popular, recently updated,
/// beginner, official, verified. They differ in what they select, never in how they are filtered or
/// paged, so one binding record serves all of them and a new shelf is one query handler rather than
/// a second copy of these eight properties.
/// </para>
/// <para>
/// Every value type is nullable, for the reason <see cref="SearchKnowledgeRequest"/> documents at
/// length.
/// </para>
/// </summary>
public sealed record BrowseKnowledgeRequest
{
    /// <summary>ISO 3166-1 alpha-2. Absent browses every country.</summary>
    public string? CountryCode { get; init; }

    /// <summary>A navigation row key. Expanded server-side into its source guide categories.</summary>
    public string? CategoryKey { get; init; }

    /// <summary>An ISO 639-1 code.</summary>
    public string? Language { get; init; }

    /// <summary>A BCP-47 tag, for the labels in the answer.</summary>
    public string? Locale { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
