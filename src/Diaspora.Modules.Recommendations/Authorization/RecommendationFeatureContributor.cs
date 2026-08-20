using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Recommendations.Authorization;

/// <summary>
/// Declares the feature gates the Recommendation Platform owns.
/// <para>
/// <b>The feed gate is at <see cref="AccessTier.Onboarding"/>, the lowest tier the platform grants
/// anything at, and that is the whole cold-start argument made concrete.</b> A member halfway
/// through registration is exactly the member with no history, in a country they have just arrived
/// in, who most needs to be told which guide to read first. Gating the feed above this would mean the
/// cold-start path — the reason this module handles a member with no signals at all — could never run
/// for the people it was written for.
/// </para>
/// <para>
/// <b>Not <c>Anonymous</c>, which B2.0.1 settled.</b> Nothing is granted at Anonymous, because there
/// is no authenticated subject to grant it to. Recommendations are personal and need an account;
/// <c>Onboarding</c> is the tier that says "there is an account, and it is not finished yet".
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class RecommendationFeatureContributor : IFeatureContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            RecommendationFeatures.View, Module, "See recommendations across the platform.")
        {
            // See the class summary. Deliberately the lowest tier the platform grants anything at.
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            RecommendationFeatures.Personalization,
            Module,
            "Read and change how your recommendations are personalised.")
        {
            // A member must be able to turn personalization off from the moment it is on, which is
            // the moment they register. Gating the control above the thing it controls would be a
            // privacy failure with a tier floor for a cause.
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            RecommendationFeatures.Signals, Module, "Record what you looked at.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            RecommendationFeatures.Strategies, Module, "Tune how recommendations are ranked.")
        {
            MinimumTier = AccessTier.Limited,

            // Staff-only rather than tier-gated, which is B2.0.1's distinction: a tier measures how
            // far a member has progressed, a role grants a privilege. Telling an ordinary member
            // that verifying unlocks ranking control would be telling them verification lets them
            // decide what everybody sees.
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            RecommendationFeatures.Policies, Module, "Configure what may be recommended.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            RecommendationFeatures.Analytics, Module, "Read recommendation analytics.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
