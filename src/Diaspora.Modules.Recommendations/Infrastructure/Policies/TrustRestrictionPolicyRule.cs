using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Honour the trust standing a candidate's own module requires.
/// <para>
/// <b>Enforcing somebody else's rule, never inventing one.</b> When Housing publishes a listing as
/// verified-members-only, that is Housing's decision about its own data and this module's job is to
/// not undermine it. A recommendation that surfaces a listing the member cannot open is worse than
/// no recommendation: it is a promise the platform immediately breaks.
/// </para>
/// <para>
/// <b>A system policy.</b> Disabling it would let this module recommend content past the visibility
/// its owner declared, and the resulting 403 would look like a bug in the module that was right.
/// </para>
/// </summary>
public sealed class TrustRestrictionPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "trust-restriction";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.TrustRestriction;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var profile = context.Personalization.Profile;

        if (candidate.Visibility == CandidateVisibility.Verified && !profile.IsVerified)
        {
            return PolicyDecision.Exclude("the item is visible to verified members only");
        }

        if (candidate.MinimumTrustLevel > profile.TrustLevel)
        {
            return PolicyDecision.Exclude(
                $"the item requires trust level {candidate.MinimumTrustLevel}");
        }

        return PolicyDecision.Allow();
    }
}
