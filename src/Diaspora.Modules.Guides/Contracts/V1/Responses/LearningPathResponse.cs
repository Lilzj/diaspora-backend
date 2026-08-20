namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// A curated route through one country's knowledge, with its steps resolved.
/// <para>
/// <see cref="RequiredStepCount"/> is separate from <see cref="StepCount"/> because a progress
/// indicator must divide by the first. A path with seven steps of which two are optional is "3 of
/// 5" for somebody who skipped them, and reporting only the total would tell a reader they were
/// permanently incomplete for steps that never applied to them.
/// </para>
/// </summary>
public sealed record LearningPathResponse
{
    public required Guid LearningPathId { get; init; }

    /// <summary>The stable key. Unique within its country, and what a deep link carries.</summary>
    public required string Key { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    /// <summary>The language the path's own copy is written in.</summary>
    public string? Language { get; init; }

    /// <summary>One of <c>draft</c>, <c>published</c>, <c>archived</c>.</summary>
    public required string Status { get; init; }

    public required int StepCount { get; init; }

    /// <summary>Steps that count towards completion. The denominator of a progress indicator.</summary>
    public required int RequiredStepCount { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The optimistic concurrency token. Sent back on an arrangement.</summary>
    public required int Revision { get; init; }

    public IReadOnlyList<LearningPathStepResponse> Steps { get; init; } = [];
}
