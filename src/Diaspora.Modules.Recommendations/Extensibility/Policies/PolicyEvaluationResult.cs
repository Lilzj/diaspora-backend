using Diaspora.Modules.Recommendations.Domain.Candidates;

namespace Diaspora.Modules.Recommendations.Extensibility.Policies;

/// <summary>
/// What survived the policy pass, and what did not.
/// <para>
/// <b>The exclusion tally is not diagnostics decoration; it is the only way to tell two very
/// different failures apart.</b> A surface returning four items because the country has four
/// recommendable jobs and a surface returning four items because a misconfigured trust policy
/// removed ninety-six look identical from outside. This carries the difference all the way to the
/// stored set and the operator endpoint.
/// </para>
/// </summary>
public sealed record PolicyEvaluationResult
{
    /// <summary>What may be ranked.</summary>
    public required IReadOnlyList<RecommendationCandidate> Allowed { get; init; }

    /// <summary>Demotion factors by candidate key, for the ones a policy scored down but kept.</summary>
    public required IReadOnlyDictionary<string, decimal> DemotionFactors { get; init; }

    /// <summary>How many candidates each policy key removed. See the class summary.</summary>
    public required IReadOnlyDictionary<string, int> ExclusionsByPolicy { get; init; }

    public int ExcludedCount => ExclusionsByPolicy.Values.Sum();

    public static PolicyEvaluationResult Unfiltered(IReadOnlyList<RecommendationCandidate> candidates) =>
        new()
        {
            Allowed = candidates,
            DemotionFactors = new Dictionary<string, decimal>(StringComparer.Ordinal),
            ExclusionsByPolicy = new Dictionary<string, int>(StringComparer.Ordinal)
        };
}
