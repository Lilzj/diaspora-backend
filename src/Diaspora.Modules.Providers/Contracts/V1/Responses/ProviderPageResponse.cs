namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One page of providers.
/// <para>
/// <b>Cursor-paged, not offset-paged</b>, and the difference is correctness rather than speed.
/// An offset page over a table being written to skips and repeats rows: a provider published
/// between page one and page two shifts everything down, so a member scrolling sees one provider
/// twice and never sees another. On an infinite-scroll list — which is exactly what the client's
/// discovery tab is — that happens constantly.
/// </para>
/// <para>
/// <see cref="TotalCount"/> is nullable and off by default. Counting is the expensive half of a
/// paged query and almost nothing renders the number; a caller that genuinely needs it asks.
/// </para>
/// </summary>
public sealed record ProviderPageResponse
{
    public required IReadOnlyList<ProviderSummaryResponse> Items { get; init; }

    /// <summary>Opaque. Hand it back to fetch the next page; null when there is none.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>Null unless the caller asked for it.</summary>
    public int? TotalCount { get; init; }

    /// <summary>
    /// The order the page is actually in. <c>Distance</c> means nearest first across every page;
    /// a distance search that could not be ordered that way — no radius, or too many providers to
    /// measure — reads <c>Recommended</c>, so a client can say so rather than imply it.
    /// </summary>
    public string? AppliedSort { get; init; }
}
