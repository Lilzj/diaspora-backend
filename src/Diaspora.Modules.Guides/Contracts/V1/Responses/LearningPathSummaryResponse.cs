namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// A learning path without its steps — what a country page lists.
/// <para>
/// Separate from the full response for the same reason a guide card is separate from a guide: a
/// country dashboard listing four paths that each resolved seven guides would be twenty-eight guide
/// lookups to render four titles.
/// </para>
/// </summary>
public sealed record LearningPathSummaryResponse
{
    public required Guid LearningPathId { get; init; }

    public required string Key { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    /// <summary>One of <c>draft</c>, <c>published</c>, <c>archived</c>.</summary>
    public required string Status { get; init; }

    public required int StepCount { get; init; }

    /// <summary>Steps that count towards completion.</summary>
    public required int RequiredStepCount { get; init; }
}
