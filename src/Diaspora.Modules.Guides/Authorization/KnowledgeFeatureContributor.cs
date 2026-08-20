using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the feature gates the Knowledge Directory owns.
/// <para>
/// Two gates, both staff-only, because the only controls a client might offer and be refused are the
/// curation ones. Reading a country's knowledge carries no gate at all: public knowledge is public,
/// and the anonymous read path has no authorization requirement to grey anything out against.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class KnowledgeFeatureContributor : IFeatureContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            KnowledgeFeatures.Curate, Module, "Curate country knowledge directories.")
        {
            // Limited with the staff flag, rather than Verified: a Verified floor would tell a
            // member that verifying their identity unlocks curation. It does not.
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        yield return new FeatureDefinition(
            KnowledgeFeatures.ManagePaths, Module, "Write curated onboarding routes.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };
    }
}
