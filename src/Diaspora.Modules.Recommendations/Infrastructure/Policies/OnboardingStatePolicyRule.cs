using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Do not recommend a member something they cannot act on yet.
/// <para>
/// <b>A demotion, not an exclusion, and the choice took some thought.</b> A member halfway through
/// onboarding cannot apply for a job — the feature gate refuses — so recommending one leads to a
/// locked screen. But hiding jobs entirely would leave them with no idea the platform has any, which
/// is worse: the point of showing somebody a locked capability is that it tells them what finishing
/// onboarding is for.
/// </para>
/// <para>
/// So the transactional types are scored down rather than removed, and the guides that explain how
/// to finish onboarding rise past them naturally. The member sees both, in the order that helps.
/// </para>
/// <para>
/// The demotion factor is a catalogue parameter, so a deployment that would rather hide them
/// entirely sets it low — and does so visibly, in a row somebody can find.
/// </para>
/// </summary>
public sealed class OnboardingStatePolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "onboarding-state";

    /// <summary>Types an unfinished member cannot act on. See the class summary.</summary>
    private static readonly RecommendationTargetType[] RequiresOnboarding =
    [
        RecommendationTargetType.Job,
        RecommendationTargetType.MarketplaceListing,
        RecommendationTargetType.HousingListing
    ];

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.OnboardingState;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        if (context.Personalization.Profile.HasCompletedOnboarding)
        {
            return PolicyDecision.Allow();
        }

        if (!RequiresOnboarding.Contains(candidate.Target.TargetType))
        {
            return PolicyDecision.Allow();
        }

        return PolicyDecision.Demote(
            context.Configuration.DemotionFactor,
            "the member has not finished onboarding and cannot act on this yet");
    }
}
