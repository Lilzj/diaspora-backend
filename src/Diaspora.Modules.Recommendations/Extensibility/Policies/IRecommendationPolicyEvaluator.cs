using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Policies;

/// <summary>
/// Runs every enabled policy over a pool, once.
/// <para>
/// <b>One pass over the pool rather than one pass per rule, and the difference matters at four
/// hundred candidates and nine rules.</b> Rule-major evaluation walks the pool nine times and cannot
/// short-circuit: a candidate excluded by the first rule is still evaluated by the other eight.
/// Candidate-major stops at the first exclusion, which on a real pool is most of the work.
/// </para>
/// </summary>
public interface IRecommendationPolicyEvaluator
{
    /// <summary>
    /// Filters the pool and reports what was removed and why.
    /// <para>
    /// Never throws on a misconfigured rule. A catalogue row naming an unimplemented key is logged
    /// and skipped rather than failing the request — a member's feed should not go blank because
    /// somebody mistyped a policy key, and the startup validator has already made that loud.
    /// </para>
    /// </summary>
    Task<PolicyEvaluationResult> EvaluateAsync(
        IReadOnlyList<RecommendationCandidate> candidates,
        PersonalizationContext context,
        CancellationToken cancellationToken = default);
}
