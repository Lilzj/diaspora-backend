using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Catalog;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;

namespace Diaspora.Modules.Recommendations.Catalog;

/// <summary>
/// The policies the platform ships with.
/// <para>
/// <b>Five of the nine are system policies and cannot be disabled, and that split is the most
/// consequential judgement in the catalogue.</b> A system policy protects a guarantee: never
/// recommend blocked content, never recommend somebody their own listing, never recommend something
/// that has ended, never show a signed-out caller private content, never breach the visibility a
/// vertical declared. Each of those, if configurable, would be one row away from a defect that looks
/// like a setting.
/// </para>
/// <para>
/// <b>The four that are not are genuine product and legal decisions.</b> Whether a member who has not
/// finished onboarding sees jobs, how aggressively country restrictions apply, how long a dismissal
/// lasts, whether a muted category is absolute — these differ by deployment and by jurisdiction and
/// will change without a release.
/// </para>
/// <para>
/// <b>The order is cheapest-first, and it is a performance decision.</b> The evaluator walks a
/// candidate through the rules and stops at the first exclusion, so a set lookup should run before
/// anything that reads a nested collection.
/// </para>
/// </summary>
public sealed class DefaultRecommendationPolicyContributor : IRecommendationPolicyContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<RecommendationPolicySeed> GetPolicies()
    {
        // ── Two hash lookups. Cheapest, and the one that must never be skipped. ──────────────
        yield return new RecommendationPolicySeed
        {
            Key = BlockedContentPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.BlockedContent,
            Effect = PolicyEffect.Exclude,
            Description = "Never recommend content the member blocked, or content by an author they blocked.",
            Order = 10,
            IsSystem = true
        };

        // ── A set lookup. ────────────────────────────────────────────────────────────────────
        yield return new RecommendationPolicySeed
        {
            Key = DismissalPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.Dismissal,
            Effect = PolicyEffect.Exclude,
            Description = "Do not show a member something they dismissed inside the suppression window.",
            Order = 20
        };

        yield return new RecommendationPolicySeed
        {
            Key = SelfAuthoredPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.SelfAuthored,
            Effect = PolicyEffect.Exclude,
            Description = "Never recommend a member their own listing, guide, event or job.",
            Order = 30,
            IsSystem = true
        };

        // ── Two field comparisons. ───────────────────────────────────────────────────────────
        yield return new RecommendationPolicySeed
        {
            Key = AvailabilityPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.Availability,
            Effect = PolicyEffect.Exclude,
            Description = "Never recommend something that has been withdrawn or has expired.",
            Order = 40,
            IsSystem = true
        };

        yield return new RecommendationPolicySeed
        {
            Key = AccessTierPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.AccessTier,
            Effect = PolicyEffect.Exclude,
            Description = "A signed-out caller may be shown public content and nothing else.",
            Order = 50,
            IsSystem = true
        };

        yield return new RecommendationPolicySeed
        {
            Key = TrustRestrictionPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.TrustRestriction,
            Effect = PolicyEffect.Exclude,
            Description = "Honour the trust standing a candidate's own module requires.",
            Order = 60,
            IsSystem = true
        };

        yield return new RecommendationPolicySeed
        {
            Key = CountryVisibilityPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.CountryVisibility,
            Effect = PolicyEffect.Exclude,
            Description = "Honour a candidate's country restriction.",
            Order = 70,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // The permissive reading: a member whose country is not yet known is not excluded.
                // A deployment with a stricter legal position sets this to true. See the rule.
                [CountryVisibilityPolicyRule.RequireCountryParameter] = "false"
            }
        };

        // ── Reads a nested collection. Last of the exclusions. ───────────────────────────────
        yield return new RecommendationPolicySeed
        {
            Key = MutedCategoryPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.MutedCategory,
            Effect = PolicyEffect.Exclude,
            Description = "Honour a category the member muted.",
            Order = 80
        };

        // ── The audit point. Excludes nothing but a surface the member switched off. ─────────
        yield return new RecommendationPolicySeed
        {
            Key = PrivacyPolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.Privacy,
            Effect = PolicyEffect.Exclude,
            Description = "Honour a member who turned personalization off for a surface.",
            Order = 90,
            IsSystem = true
        };

        // ── The only demotion the platform ships. ────────────────────────────────────────────
        yield return new RecommendationPolicySeed
        {
            Key = OnboardingStatePolicyRule.PolicyKey,
            Kind = RecommendationPolicyKind.OnboardingState,
            Effect = PolicyEffect.Demote,

            // Scored down hard, not hidden. A member who cannot yet apply for a job should still see
            // that the platform has jobs — that is what tells them what finishing onboarding is for.
            DemotionFactor = 0.35m,
            Description = "Score down what a member cannot act on until onboarding is finished.",
            Order = 100
        };
    }
}
