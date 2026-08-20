using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;

namespace Diaspora.Modules.Recommendations.Extensibility.Engine;

/// <summary>
/// One ranked candidate, before it becomes a stored item.
/// <para>
/// <b>Carries the candidate rather than a reference, because the caller has to render something and
/// re-reading four hundred rows to display twenty would be absurd.</b> The stored
/// <c>RecommendationItem</c> keeps only the reference — the record of what was recommended does not
/// need a copy of the title — but the in-flight result keeps the projection so the mapper has
/// everything a card needs without a second query.
/// </para>
/// </summary>
public sealed record ScoredRecommendation
{
    public required RecommendationCandidate Candidate { get; init; }

    /// <summary>The weighted sum after any demotion. Between 0 and 1.</summary>
    public required decimal Score { get; init; }

    /// <summary>Whichever strategy contributed most.</summary>
    public required string PrimaryStrategyKey { get; init; }

    /// <summary>Why. Built by the reason builder from the contributions.</summary>
    public required RecommendationExplanation Explanation { get; init; }

    /// <summary>The per-strategy arithmetic, in descending order of contribution.</summary>
    public required IReadOnlyList<StrategyContribution> Contributions { get; init; }

    /// <summary>What a policy multiplied the score by. One when nothing demoted it.</summary>
    public decimal DemotionFactor { get; init; } = 1m;
}
