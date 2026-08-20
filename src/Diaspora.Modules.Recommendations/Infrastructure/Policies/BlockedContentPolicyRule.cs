using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Never recommend something the member blocked, or anything by somebody they blocked.
/// <para>
/// <b>A system policy: it cannot be disabled, and the aggregate refuses rather than the API
/// validator.</b> A platform that can be configured to recommend somebody the seller they blocked
/// has a defect, not a setting — and the configuration that did it would be one row nobody would
/// think to look at.
/// </para>
/// <para>
/// <b>Two block lists, two owners, one rule.</b> A blocked listing is this module's own record; a
/// blocked person is Community's, and arrives through a published contract. Enforcing both here is
/// what makes "I blocked this person" mean something on a surface Community does not own.
/// </para>
/// <para>
/// Ordered first among the policies. It is the cheapest check in the set — two hash lookups — and
/// running it first means every more expensive rule skips a candidate that was never eligible.
/// </para>
/// </summary>
public sealed class BlockedContentPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "blocked-content";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.BlockedContent;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var preference = context.Personalization.Preference;

        if (preference is not null &&
            preference.IsBlocked(candidate.Target.TargetType, candidate.Target.TargetId))
        {
            return PolicyDecision.Exclude("the member blocked this item");
        }

        if (candidate.OwnerUserId is not null &&
            context.BlockedAuthorIds.Contains(candidate.OwnerUserId.Value))
        {
            return PolicyDecision.Exclude("the member blocked this author");
        }

        return PolicyDecision.Allow();
    }
}
