using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Honour a member who turned personalization off.
/// <para>
/// <b>It excludes nothing, and that is exactly right.</b> A member who opted out did not ask to be
/// shown less — they asked not to be profiled. Excluding candidates would be the platform punishing
/// them for the choice; what actually has to happen is that their behaviour stops being used, which
/// is enforced upstream by <c>PersonalizationContext.IsSurfacePersonalized</c> and by every strategy
/// that reads it.
/// </para>
/// <para>
/// <b>So why does the rule exist at all?</b> Because "every strategy checks the flag" is a
/// convention, and a future ranking adapter written by somebody who has not read this paragraph will
/// not check it. This rule is the audit point: it records in the diagnostics that an opted-out
/// member's page was generated without personalization, so a page that was personalised anyway is
/// visible as a contradiction rather than invisible as a silent leak.
/// </para>
/// <para>
/// The one thing it does enforce is a surface the member switched off. A request for a surface with
/// personalization disabled returns the un-personalised page, and a request for a target type the
/// member hid returns nothing — which is the only place a privacy preference legitimately removes
/// content.
/// </para>
/// </summary>
public sealed class PrivacyPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "privacy";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.Privacy;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        var personalization = context.Personalization;

        // A surface the member switched off. The only content this rule removes — see the class
        // summary.
        if (personalization.Preference is { } preference &&
            !preference.IsSurfaceEnabled(personalization.Surface) &&
            personalization.Surface is not (RecommendationSurface.HomeFeed or RecommendationSurface.Discover))
        {
            return PolicyDecision.Exclude(
                $"the member turned off recommendations on the {personalization.Surface} surface");
        }

        return PolicyDecision.Allow();
    }
}
