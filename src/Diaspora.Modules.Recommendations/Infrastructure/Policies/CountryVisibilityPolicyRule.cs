using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Honour a candidate's country restriction.
/// <para>
/// <b>The rule most likely to be a legal requirement rather than a product preference, which is why
/// it is configurable per surface and per target type.</b> Regulated categories — financial
/// services, immigration advice, healthcare — are restricted differently in different countries, and
/// the shape of the restriction is a lawyer's decision that will change without warning.
/// </para>
/// <para>
/// <b>A member with no country is not excluded, and that is the difficult call.</b> Someone
/// mid-onboarding has no country yet, and excluding country-restricted content from them would empty
/// the feed for exactly the members who most need it. The reading taken here is that
/// <see cref="CandidateVisibility.CountryOnly"/> restricts <em>known</em> mismatches: a member
/// confirmed to be elsewhere is excluded, a member whose country is not yet known is not. A
/// deployment whose legal position is stricter sets the <c>requirecountry</c> parameter and gets the
/// conservative reading.
/// </para>
/// </summary>
public sealed class CountryVisibilityPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "country-visibility";

    /// <summary>Excludes a country-restricted candidate when the member's country is unknown.</summary>
    public const string RequireCountryParameter = "requirecountry";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.CountryVisibility;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        if (candidate.Visibility != CandidateVisibility.CountryOnly || candidate.CountryCode is null)
        {
            return PolicyDecision.Allow();
        }

        var memberCountry = context.Personalization.EffectiveCountryCode;

        if (string.IsNullOrWhiteSpace(memberCountry))
        {
            var strict = string.Equals(
                context.Configuration.Parameter(RequireCountryParameter),
                "true",
                StringComparison.OrdinalIgnoreCase);

            return strict
                ? PolicyDecision.Exclude("the item is country-restricted and the member's country is unknown")
                : PolicyDecision.Allow();
        }

        return string.Equals(candidate.CountryCode, memberCountry, StringComparison.OrdinalIgnoreCase)
            ? PolicyDecision.Allow()
            : PolicyDecision.Exclude(
                $"the item is restricted to {candidate.CountryCode} and the member is in {memberCountry}");
    }
}
