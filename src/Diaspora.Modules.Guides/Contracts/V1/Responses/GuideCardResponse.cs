namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One guide on a list — discovery, browse, "mine", featured, trending. Everything a card
/// renders and nothing a detail screen needs, because a page of twenty cards must not carry
/// twenty full documents.
/// </summary>
public sealed record GuideCardResponse
{
    public required Guid GuideId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Excerpt { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public required string Language { get; init; }

    /// <summary>A <c>GuideStatus</c> name. Drafts appear only on the author's own lists.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>GuideDifficulty</c> name.</summary>
    public required string Difficulty { get; init; }

    public required int ReadingTimeMinutes { get; init; }

    public required bool IsVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public required Guid AuthorId { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? HeroMediaId { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    // ── The counters a card badges ──────────────────────────────────────────────────────────

    public required long Views { get; init; }

    public required int HelpfulCount { get; init; }

    public required int CommentCount { get; init; }

    public required int BookmarkCount { get; init; }
}
