namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One phrase people are searching for in a country's knowledge.
/// <para>
/// <b>Derived from searches, not from tags, and the difference is the whole value.</b> A trending
/// list built from what has been written tells you what the platform is good at. Built from what
/// people asked for, it tells you what they need — and the two diverge most sharply exactly when
/// something has changed, which is when a knowledge base most needs to notice.
/// </para>
/// <para>
/// <see cref="IsUnanswered"/> is the actionable field. A phrase with a high
/// <see cref="SearchCount"/> and no results is a guide that should be commissioned, named by the
/// people who wanted it.
/// </para>
/// </summary>
public sealed record TrendingTopicResponse
{
    /// <summary>The normalized phrase, exactly as it is counted.</summary>
    public required string Term { get; init; }

    public required long SearchCount { get; init; }

    /// <summary>What the most recent search for it returned.</summary>
    public required int ResultCount { get; init; }

    /// <summary>Whether the phrase currently finds nothing.</summary>
    public required bool IsUnanswered { get; init; }

    public required DateTimeOffset LastSearchedAt { get; init; }
}
