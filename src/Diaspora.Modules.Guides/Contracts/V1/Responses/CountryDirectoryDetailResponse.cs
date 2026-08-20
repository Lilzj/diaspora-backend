namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One country directory as a curator sees it: hidden rows, unpublished state, translations.
/// <para>
/// <b>A separate contract from the reader's, rather than the same one with more fields
/// populated.</b> The reader's response is filtered — hidden rows removed, unpublished directories
/// invisible — and a single contract that sometimes carried both would make every consumer's
/// rendering depend on who was asking. Two contracts make the audience explicit, and make it
/// impossible for a curator-only field to reach a reader by accident.
/// </para>
/// </summary>
public sealed record CountryDirectoryDetailResponse
{
    public required Guid DirectoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The default name, unlocalized — a curator edits this one.</summary>
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    public string? PrimaryLanguage { get; init; }

    public IReadOnlyList<string> Languages { get; init; } = [];

    public required bool IsPublished { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The optimistic concurrency token. Sent back on every write.</summary>
    public required int Revision { get; init; }

    /// <summary>The arrangement, hidden rows included.</summary>
    public IReadOnlyList<CountryDirectoryEntryResponse> Entries { get; init; } = [];

    /// <summary>The featured shelf, across every scope, as identifiers and curator notes.</summary>
    public IReadOnlyList<CountryFeaturedGuideResponse> FeaturedGuides { get; init; } = [];

    /// <summary>The header translations, for editing.</summary>
    public IReadOnlyList<CountryDirectoryLocalizationResponse> Localizations { get; init; } = [];
}
