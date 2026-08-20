namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One row of the version history list. The content stays behind the detail call.</summary>
public sealed record GuideVersionSummaryResponse
{
    public required Guid VersionId { get; init; }

    public required Guid GuideId { get; init; }

    public required int VersionNumber { get; init; }

    public required string Title { get; init; }

    /// <summary>What the editor said changed, in their own words.</summary>
    public string? RevisionNotes { get; init; }

    /// <summary>Who published this version. Usually the author; staff after a moderation edit.</summary>
    public required Guid EditorId { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required int WordCount { get; init; }

    public required int ReadingTimeMinutes { get; init; }
}
