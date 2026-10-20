namespace Diaspora.Contracts.Guides.V1.Authors;

/// <summary>
/// What one author's published guides have gathered, for modules that compose after Guides.
/// <para>
/// Added 21 Oct 2026 for the creator reputation read. Guides owns the guide rows and their
/// engagement counters and implements this; Creator reads it without referencing Guides.
/// </para>
/// </summary>
public interface IAuthorGuideStatsReader
{
    Task<AuthorGuideStats> GetAsync(Guid authorUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Totals over the author's <em>published</em> guides — the set a reader can open. Drafts, archived
/// and removed guides gathered nothing a reader can see, and are not counted.
/// </summary>
public sealed record AuthorGuideStats
{
    public required int PublishedGuides { get; init; }

    /// <summary>Every recorded open, including repeats.</summary>
    public required long Views { get; init; }

    /// <summary>Opens the client reported as a reader's first. An approximation by design.</summary>
    public required long UniqueViews { get; init; }

    public required long HelpfulVotes { get; init; }

    public required long NotHelpfulVotes { get; init; }

    public required long Bookmarks { get; init; }
}
