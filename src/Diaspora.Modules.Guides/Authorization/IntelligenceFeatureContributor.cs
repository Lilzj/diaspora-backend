using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the feature gates the Country Intelligence Platform owns.
/// <para>
/// Three gates, all staff-only, because the only controls a client might offer and be refused are the
/// editorial ones. Reading a country's official information carries no gate at all: an emergency
/// number is public, and the anonymous read path has no authorization requirement to grey anything
/// out against.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class IntelligenceFeatureContributor : IFeatureContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            IntelligenceFeatures.Manage, Module, "Draft and edit official country information.")
        {
            // Limited with the staff flag, rather than Verified: a Verified floor would tell a
            // member that verifying their identity unlocks the editorial desk. It does not.
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            IntelligenceFeatures.Publish, Module, "Verify and publish official notices.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            IntelligenceFeatures.Analytics, Module, "Read country intelligence usage figures.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
