namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// One curated onboarding route, described for a consuming module.
/// <para>
/// Steps are absent by design. A consumer showing "new to this country? there is a 7-step guide"
/// needs a title, a length and a link; a consumer that wanted to render the steps itself would be
/// reimplementing the onboarding screen, which is exactly the duplication this phase exists to stop.
/// It links to the directory instead.
/// </para>
/// </summary>
public sealed record LearningPathDescriptor
{
    public required Guid LearningPathId { get; init; }

    /// <summary>The stable key. Unique within its country, and what a deep link carries.</summary>
    public required string Key { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    public required int StepCount { get; init; }

    /// <summary>Steps that count towards completion. The denominator of a progress indicator.</summary>
    public required int RequiredStepCount { get; init; }
}
