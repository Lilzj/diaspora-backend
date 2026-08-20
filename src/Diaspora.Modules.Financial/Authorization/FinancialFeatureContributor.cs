using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Financial.Authorization;

/// <summary>
/// Declares the feature gates the Financial Platform owns.
/// <para>
/// A gate is a public contract: the client holds it in a capability map and switches rendering on
/// it. These six are all new names — the host's provisional catalogue has never held a financial
/// entry, which was verified rather than assumed, because the catalogue refuses two modules
/// declaring one gate and the resulting startup failure takes down every request that resolves one.
/// </para>
/// </summary>
public sealed class FinancialFeatureContributor : IFeatureContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            FinancialFeatures.Wallet, Module, "See your balance and statement.")
        {
            MinimumTier = AccessTier.Onboarding
        };

        yield return new FeatureDefinition(
            FinancialFeatures.Payments, Module, "Pay for things on Diaspora.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            FinancialFeatures.Purchases, Module, "Buy premium features and placements.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            FinancialFeatures.Subscriptions, Module, "Hold a subscription plan.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            FinancialFeatures.Giving, Module, "Donate and contribute to community funds.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            FinancialFeatures.Operations, Module, "Operate the platform's finances.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}
