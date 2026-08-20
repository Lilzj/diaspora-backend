using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Providers.Authorization;

/// <summary>
/// Declares the feature gates the Providers module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The client already has the screen — its discovery tab wraps its results in
/// <c>&lt;FeatureGate featureName={activeTab}&gt;</c>, which passes the literal string
/// <c>"Providers"</c>, and decides locally from account state. <see cref="ProviderFeatures.Discovery"/>
/// is what lets it ask the server instead.
/// </para>
/// </summary>
public sealed class ProviderFeatureContributor : IFeatureContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            ProviderFeatures.Discovery, Module, "Browse providers near you.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Registration, Module, "List your business or organization.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Management, Module, "Manage your listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Staffing, Module, "Add managers and editors.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.ServiceCatalog, Module, "Publish your services and prices.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Availability, Module, "Set your opening hours.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Verification, Module, "Get your business verified.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Reviews, Module, "Review a provider.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Recommendations, Module, "Recommend a provider.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            ProviderFeatures.Statistics, Module, "See how your listing is performing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            ProviderFeatures.FeaturedPlacement, Module, "Be eligible for featured placement.")
        {
            // Featured placement is staff-granted, and a gate that told an unverified provider
            // it was merely locked would imply they could unlock it by verifying. They cannot.
            MinimumTier = AccessTier.Verified
        };
    }
}
