using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.GeographicIntelligence.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Reading geographic intelligence is <see cref="AccessTier.Limited"/>, matching B4's
/// hyperlocal permission rather than sitting above it.</b> B2.0.1's rule is that a tier gates
/// privilege over other members rather than progression, and nothing here is privilege over
/// anybody: every figure is about a place, composed from content that is already individually
/// discoverable. A member who has not finished verification is precisely the member deciding which
/// neighbourhood to move to.
/// </para>
/// <para>
/// <b>The capability report sits at the same tier as the surfaces it describes, not below them.</b>
/// Putting it at <see cref="AccessTier.Onboarding"/> was the first instinct — a client should
/// discover that routing is unavailable before it offers a Directions button — and it buys nothing:
/// a member one rung lower could read the report and reach none of what it reports on. B2.0.1's
/// onboarding allow-list is an explicit, reviewed specification and every entry on it removes a loop
/// with no entrance. This would have removed none, so it is not on it.
/// </para>
/// </summary>
public sealed class GeographicIntelligencePermissionContributor : IPermissionContributor
{
    public string Module => GeographicIntelligenceModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            GeographicIntelligencePermissions.IntelligenceRead,
            Module,
            "Read composed geographic intelligence for a place: score dimensions, cost of living, "
            + "safety and the density layer.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GeographicIntelligencePermissions.RoutingRead,
            Module,
            "Ask for a route between two points.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            GeographicIntelligencePermissions.CapabilitiesRead,
            Module,
            "Read which geographic providers this deployment has configured.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>
/// The module's feature gates.
/// <para>
/// Two, and the split is operational. A deployment turns the intelligence surfaces on as soon as
/// neighbourhood profiles exist to compose from; it turns routing on when a vendor is contracted.
/// Shipping one gate for both would mean a deployment with no routing vendor either hides the
/// score breakdown it can serve or advertises a Directions button it cannot answer.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceFeatureContributor : IFeatureContributor
{
    public string Module => GeographicIntelligenceModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            GeographicIntelligenceFeatures.Intelligence,
            Module,
            "Neighbourhood score breakdowns, cost of living, safety provenance and the density "
            + "layer.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            GeographicIntelligenceFeatures.Routing,
            Module,
            "Walking, driving and transit routing through a configured provider.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

public sealed class GeographicIntelligencePolicyContributor : IPolicyContributor
{
    public string Module => GeographicIntelligenceModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            GeographicIntelligencePolicies.IntelligenceRead,
            Module,
            GeographicIntelligencePermissions.IntelligenceRead);

        yield return PolicyDefinition.ForPermission(
            GeographicIntelligencePolicies.RoutingRead,
            Module,
            GeographicIntelligencePermissions.RoutingRead);

        yield return PolicyDefinition.ForPermission(
            GeographicIntelligencePolicies.CapabilitiesRead,
            Module,
            GeographicIntelligencePermissions.CapabilitiesRead);
    }
}
