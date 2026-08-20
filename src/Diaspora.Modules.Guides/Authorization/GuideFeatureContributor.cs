using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Authorization;

/// <summary>
/// Declares the feature gates the Guides module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The split mirrors the permission band: authoring, engaging and suggesting are open at
/// <see cref="AccessTier.Limited"/>, publishing and verification at <see cref="AccessTier.Verified"/>,
/// and moderation is staff-only — a gate that told a member they were merely locked out of the
/// moderation queue would imply verifying unlocks it. It does not.
/// </para>
/// <para>
/// Reading published guides needs no gate at all: public knowledge is public, and the anonymous
/// read path carries no authorization requirement.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class GuideFeatureContributor : IFeatureContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            GuideFeatures.Author, Module, "Write and edit guides.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            GuideFeatures.Publish, Module, "Publish your guide for the community.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            GuideFeatures.Engage, Module, "Vote, save and share guides.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            GuideFeatures.Suggest, Module, "Suggest improvements to a guide.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            GuideFeatures.Verify, Module, "Get your guide verified.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            GuideFeatures.Moderate, Module, "Work the guide moderation queue.")
        {
            MinimumTier = AccessTier.Limited,
            StaffOnly = true
        };

        // ── Claimed from the provisional catalogue in PSR-1 ──────────────────────────────
        //
        // Same name, same floor, same behaviour as the host declared it with since B2. The
        // matching line in ProvisionalCatalogContributor was deleted in the same commit:
        // leaving both would make two modules declare one gate, which the feature catalogue
        // refuses, and the resulting startup failure takes down every request that resolves
        // the catalogue. B8 records that trap from the other side, when guides.publish moved.
        yield return new FeatureDefinition(
            GuideFeatures.Browse, Module, "Browse published guides.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}
