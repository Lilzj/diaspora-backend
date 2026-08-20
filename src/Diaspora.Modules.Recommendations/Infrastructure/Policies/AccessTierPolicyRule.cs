using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Honour the platform's access ladder.
/// <para>
/// <b>Distinct from the trust rule, and B2.0.1 is why.</b> A tier measures how far a member has
/// progressed; trust is a standing the Trust Engine granted. They are correlated and they are not
/// the same, and the two policies fail for different reasons — a member can be verified and
/// restricted, or onboarded and unverified. Merging them into one "is this member good enough" check
/// is how the platform's tier semantics get quietly re-invented inside a module.
/// </para>
/// <para>
/// <b>Anonymous is the only tier this rule actually excludes at.</b> A signed-out caller may be shown
/// public content and nothing else. Everything above that is the individual verticals' business, and
/// the trust rule already enforces what they declared.
/// </para>
/// </summary>
public sealed class AccessTierPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "access-tier";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.AccessTier;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var tier = context.Personalization.AccessTier;

        if (tier > AccessTier.Anonymous)
        {
            return PolicyDecision.Allow();
        }

        return candidate.Visibility == CandidateVisibility.Public
            ? PolicyDecision.Allow()
            : PolicyDecision.Exclude("the caller is not signed in and the item is not public");
    }
}
