using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Never recommend somebody their own listing.
/// <para>
/// <b>The oldest bug in the genre, and it is a bug rather than a preference — which is why this is a
/// system policy.</b> It happens because everything a member wrote scores well: they engaged with it
/// (they created it), it matches their interests (they wrote it about their interests), it is in
/// their city. Every strategy independently concludes it is the best candidate on the platform for
/// them, and each is right.
/// </para>
/// <para>
/// <b>Two checks, and both are needed.</b> The owner recorded on the projection catches the common
/// case; the engaged-target set catches the one where the owning module did not publish an owner but
/// the member has obviously interacted with their own item.
/// </para>
/// </summary>
public sealed class SelfAuthoredPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "self-authored";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.SelfAuthored;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        if (candidate.OwnerUserId is not null &&
            candidate.OwnerUserId.Value == context.Personalization.UserId.Value)
        {
            return PolicyDecision.Exclude("the member owns this item");
        }

        return context.OwnedTargetKeys.Contains(candidate.Target.Key)
            ? PolicyDecision.Exclude("the member owns this item")
            : PolicyDecision.Allow();
    }
}
