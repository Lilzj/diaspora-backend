using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Do not show somebody the thing they just swiped away.
/// <para>
/// <b>The single most noticeable policy in the module, because its failure is immediately
/// visible.</b> A member dismisses a card, pulls to refresh, and sees the same card: every other
/// piece of sophistication in the platform is discredited in that moment, because the system has
/// visibly failed to do the simplest thing it was asked.
/// </para>
/// <para>
/// <b>It reads a pre-computed set rather than querying, and that is not a micro-optimisation.</b>
/// The check runs against every candidate in the pool on every generation; a query per candidate
/// would be four hundred reads for one page, on the path that runs on every app launch. The set is
/// assembled once by the engine.
/// </para>
/// <para>
/// <b>The suppression expires, and the block does not.</b> A dismissal is "not now" — a member who
/// swiped away one flat on a bad Tuesday should see it again in three months. Somebody who wants
/// permanence uses a block, which is a different record with different semantics. See
/// <c>BlockedRecommendationTarget</c>.
/// </para>
/// </summary>
public sealed class DismissalPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "dismissal";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.Dismissal;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        return context.SuppressedTargetKeys.Contains(candidate.Target.Key)
            ? PolicyDecision.Exclude("the member dismissed this recently")
            : PolicyDecision.Allow();
    }
}
