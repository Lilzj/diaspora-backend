namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// The full guide, as the detail screen renders it.
/// <para>
/// Enums are strings, identifiers are <see cref="Guid"/>s, media and providers are references
/// — nothing here requires the reader to know this module's domain types. Provider cards are
/// hydrated through B7's directory at read time and carried on the references, never copied
/// into storage.
/// </para>
/// </summary>
public sealed record GuideResponse
{
    public required Guid GuideId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>What a card shows. Derived from content when the author wrote no summary.</summary>
    public string? Excerpt { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public required string Language { get; init; }

    /// <summary>Guides sharing this value are translations of each other.</summary>
    public required Guid TranslationGroupId { get; init; }

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    /// <summary>A <c>GuideStatus</c> name.</summary>
    public required string Status { get; init; }

    /// <summary>A <c>GuideVisibility</c> name.</summary>
    public required string Visibility { get; init; }

    /// <summary>A <c>GuideModerationState</c> name.</summary>
    public required string ModerationState { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? FirstPublishedAt { get; init; }

    public DateTimeOffset? ScheduledPublishAt { get; init; }

    public DateTimeOffset? ArchivedAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; init; }

    // ── Location ────────────────────────────────────────────────────────────────────────────

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>The owner's pin, in WGS84 degrees. Null when none was given.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    // ── Reading ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A <c>GuideDifficulty</c> name.</summary>
    public required string Difficulty { get; init; }

    public required int ReadingTimeMinutes { get; init; }

    public int? EstimatedCompletionMinutes { get; init; }

    public required int WordCount { get; init; }

    // ── Versioning & concurrency ────────────────────────────────────────────────────────────

    /// <summary>The published version number. Zero until the first publication.</summary>
    public required int CurrentVersion { get; init; }

    /// <summary>The optimistic concurrency token the editor sends back as <c>baseRevision</c>.</summary>
    public required int Revision { get; init; }

    // ── Authorship ──────────────────────────────────────────────────────────────────────────

    public required Guid AuthorId { get; init; }

    /// <summary>Trust's standing for the author, when the projection holds one.</summary>
    public GuideAuthorResponse? Author { get; init; }

    // ── Content & references ────────────────────────────────────────────────────────────────

    public IReadOnlyList<GuideSectionResponse> Sections { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<GuideMediaReferenceResponse> MediaReferences { get; init; } = [];

    public IReadOnlyList<GuideProviderReferenceResponse> Providers { get; init; } = [];

    // ── Trust, engagement, SEO ──────────────────────────────────────────────────────────────

    public required GuideTrustMarkResponse TrustMark { get; init; }

    public required GuideEngagementResponse Engagement { get; init; }

    public GuideSeoResponse? Seo { get; init; }

    /// <summary>
    /// What the signed-in reader has done to this guide: saved it, voted on it. Null when signed
    /// out. Added 4 Nov 2026: the Save button could not say it was saved, and the helpful vote
    /// forgot itself when the screen closed.
    /// </summary>
    public GuideViewerResponse? Viewer { get; init; }
}

/// <summary>The reader's own state on a guide.</summary>
public sealed record GuideViewerResponse
{
    public required bool IsSaved { get; init; }

    /// <summary><c>Helpful</c>, <c>NotHelpful</c>, or null when they have not voted.</summary>
    public string? Vote { get; init; }
}
