using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Policies;

/// <summary>
/// One rule about what a member may be shown.
/// <para>
/// <b>Kept structurally separate from <see cref="Ranking.IRankingStrategy"/>, and an architecture
/// test asserts no type implements both.</b> The brief asks for policies to remain independent of
/// ranking, and the only way to make that stick is to make the combination a build failure — because
/// the pressure to merge them is constant and always reasonable-sounding: a demotion looks like a
/// negative weight, an exclusion looks like a weight of zero, and neither is true. A weight of zero
/// still shows a candidate when nothing else is available; an exclusion never does.
/// </para>
/// <para>
/// <b>Synchronous, unlike a strategy, and that asymmetry is deliberate.</b> Everything a policy needs
/// is in its context by construction. A policy that had to await something would be a policy issuing
/// a network call per candidate on the path that runs on every app launch — so the seam does not
/// offer the option, and a future rule needing external state must pre-fetch it into the context
/// instead.
/// </para>
/// <para>
/// <b>Per candidate rather than per pool</b>, again unlike a strategy, because a policy decision
/// never depends on the other candidates. Normalisation needs the pool; "is this blocked" does not.
/// </para>
/// </summary>
public interface IRecommendationPolicyRule
{
    /// <summary>The key a catalogue row resolves this by. Unique across the platform.</summary>
    string Key { get; }

    /// <summary>The family this belongs to. The catalogue validates a row's kind against this.</summary>
    RecommendationPolicyKind Kind { get; }

    /// <summary>
    /// Decides one candidate.
    /// <para>
    /// Must be free of side effects and must not query. See the class summary.
    /// </para>
    /// </summary>
    PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context);
}
