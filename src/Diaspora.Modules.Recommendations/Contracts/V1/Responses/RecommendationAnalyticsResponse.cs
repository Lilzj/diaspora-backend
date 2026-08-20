namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One day's measured performance for one surface.
/// <para>
/// <b><see cref="AverageProfileCompleteness"/> is what makes every other number here
/// interpretable.</b> A 2% acceptance rate on a surface whose members average 15% completeness is a
/// cold-start problem; the same number at 80% is a ranking problem, and the two call for opposite
/// responses. Reporting the rate without it is reporting half an answer.
/// </para>
/// </summary>
public sealed record RecommendationAnalyticsResponse
{
    public required DateOnly Day { get; init; }

    public required string Surface { get; init; }

    public required long GeneratedSetCount { get; init; }

    public required long GeneratedItemCount { get; init; }

    public required long ViewCount { get; init; }

    public required long ClickCount { get; init; }

    public required long AcceptCount { get; init; }

    public required long DismissCount { get; init; }

    public required long BookmarkCount { get; init; }

    public required long ColdStartSetCount { get; init; }

    /// <summary>How many candidates policies removed. What explains a thin page.</summary>
    public required long PolicyExclusionCount { get; init; }

    public required decimal AcceptanceRate { get; init; }

    public required decimal ClickThroughRate { get; init; }

    public required decimal DismissalRate { get; init; }

    /// <summary>Distinct targets over items shown. One means every card was different.</summary>
    public required decimal DiversityScore { get; init; }

    /// <summary>Cold-start sets that led to an acceptance, over cold-start sets.</summary>
    public required decimal ColdStartSuccessRate { get; init; }

    /// <summary>See the class summary. The number that makes the rest interpretable.</summary>
    public required decimal AverageProfileCompleteness { get; init; }

    public required decimal AverageGenerationMilliseconds { get; init; }

    /// <summary>The slowest single generation of the day. A mean hides exactly what matters.</summary>
    public required int MaxGenerationMilliseconds { get; init; }

    /// <summary>Mean age of the newest signal at generation time. Answers "is the profile keeping up".</summary>
    public required decimal AverageSignalFreshnessHours { get; init; }

    public required IReadOnlyList<AnalyticsBreakdownResponse> Breakdowns { get; init; }
}
