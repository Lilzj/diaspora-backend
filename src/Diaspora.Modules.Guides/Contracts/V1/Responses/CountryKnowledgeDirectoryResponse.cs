namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One country's knowledge directory — the whole country page in one response.
/// <para>
/// <b>Composed server-side, and that is the point of the phase.</b> The alternative is a client
/// that fetches a directory, then a catalogue, then a guide count per row, then a featured shelf —
/// twenty-odd round trips to render one screen, reimplemented in every client, each with its own
/// idea of what the fallback label is. Every consumer of this contract gets the same page, and a
/// future client gets it without knowing how any of it was assembled.
/// </para>
/// <para>
/// Everything is already localized for the caller and already filtered for what a reader may see.
/// </para>
/// </summary>
public sealed record CountryKnowledgeDirectoryResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>Already localized.</summary>
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    /// <summary>The language most guides here are written in. A hint, never a filter.</summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>The other languages this country's knowledge is written in.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    public required bool IsPublished { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The optimistic concurrency token. Sent back on an arrangement.</summary>
    public required int Revision { get; init; }

    /// <summary>The navigation, in curated order. Hidden rows are absent for readers.</summary>
    public IReadOnlyList<CountryDirectoryEntryResponse> Categories { get; init; } = [];

    /// <summary>The country's own featured shelf — guides not scoped to any one category.</summary>
    public IReadOnlyList<GuideCardResponse> FeaturedGuides { get; init; } = [];

    /// <summary>Published onboarding routes for this country, without their steps.</summary>
    public IReadOnlyList<LearningPathSummaryResponse> LearningPaths { get; init; } = [];

    /// <summary>The headline numbers. Always present, zeroed for a country with nothing in it.</summary>
    public required KnowledgeStatisticsResponse Statistics { get; init; }
}
