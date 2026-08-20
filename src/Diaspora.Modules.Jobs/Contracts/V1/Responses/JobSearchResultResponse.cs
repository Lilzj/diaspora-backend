namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// A page of search results, with the facet counts beside them.
/// <para>
/// <see cref="TotalCount"/> is nullable because a real engine's total is an estimate and some
/// cannot produce one without a second pass. Null renders as "many", which is a better answer
/// than a number that is confidently wrong.
/// </para>
/// <para>
/// <see cref="Facets"/> may be empty. An engine that cannot compute facets cheaply says so
/// rather than computing them expensively, and the client renders filters without counts.
/// </para>
/// </summary>
public sealed record JobSearchResultResponse
{
    public required IReadOnlyList<JobCardResponse> Items { get; init; }

    /// <summary>Opaque. Send it back to get the next page.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>An estimate, or null when the engine cannot say. Never render as exact.</summary>
    public long? TotalCount { get; init; }

    public JobSearchFacetsResponse? Facets { get; init; }
}
