namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One immutable version, content included — what a reader was shown at that publication,
/// which is a historical fact and never changes.
/// </summary>
public sealed record GuideVersionResponse
{
    public required Guid VersionId { get; init; }

    public required Guid GuideId { get; init; }

    public required int VersionNumber { get; init; }

    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>Lower-case ISO 639-1, as it stood at publication.</summary>
    public required string Language { get; init; }

    public required string CategoryKey { get; init; }

    public string? RevisionNotes { get; init; }

    public required Guid EditorId { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required int WordCount { get; init; }

    public required int ReadingTimeMinutes { get; init; }

    /// <summary>The sections as they stood, deserialized from the version's snapshot.</summary>
    public IReadOnlyList<GuideSectionResponse> Sections { get; init; } = [];
}
