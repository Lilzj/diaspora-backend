namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// The guide's accumulated usage, for its author and staff. Measurement, not state — unique
/// views are an approximation by design.
/// </summary>
public sealed record GuideAnalyticsResponse
{
    public required Guid GuideId { get; init; }

    public required long Views { get; init; }

    public required long UniqueViews { get; init; }

    /// <summary>Times a reader reached the end, when the client reported it.</summary>
    public required long Completions { get; init; }

    public required long TotalReadSeconds { get; init; }

    public required int Bookmarks { get; init; }

    public required int Shares { get; init; }

    public required int HelpfulVotes { get; init; }

    public required int NotHelpfulVotes { get; init; }

    public required long SearchImpressions { get; init; }

    public required long SearchClicks { get; init; }

    /// <summary>Times an old version was restored. A signal that recent edits made things worse.</summary>
    public required int VersionAdoptions { get; init; }

    public DateTimeOffset? LastViewedAt { get; init; }
}
