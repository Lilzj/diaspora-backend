using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Housing.Authorization;

/// <summary>
/// Declares the feature gates the Housing module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The split mirrors the permission band: browsing and contributing are open at
/// <see cref="AccessTier.Limited"/>; listing, viewing and reserving are
/// <see cref="AccessTier.Verified"/>; moderation is staff-only — a gate that told a member they
/// were merely locked out of the moderation queue would imply verifying unlocks it. It does not.
/// </para>
/// <para>
/// <b>Unlike B8, B9 and B10, this module collides with nothing in the provisional catalogue.</b>
/// The host has never declared a housing gate, so B11 deletes nothing on its way in. The check was
/// made rather than assumed: three consecutive phases have recorded that two modules declaring one
/// gate fails the catalogue at startup and takes down every request that resolves it, phone
/// verification included.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class HousingFeatureContributor : IFeatureContributor
{
    public string Module => HousingModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            HousingFeatures.Browse, Module, "Browse housing and neighbourhoods.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            HousingFeatures.Contribute, Module,
            "Share what you know about a neighbourhood.")
        {
            // Limited, because the thing that makes neighbourhood insights worth having is that
            // ordinary residents write them. Everything they write is moderated regardless.
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            HousingFeatures.List, Module, "List a property to let.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            HousingFeatures.Viewing, Module, "Arrange to view a property.")
        {
            // The gate that guards addresses. See HousingPermissionContributor.
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            HousingFeatures.Reserve, Module, "Reserve a home.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            HousingFeatures.Moderate, Module, "Work the housing moderation queue.")
        {
            MinimumTier = AccessTier.Limited,

            // Staff-only rather than tier-gated, and the distinction is B2.0.1's: a tier measures
            // how far a member has progressed, a role grants a privilege. A gate that merely
            // raised the tier would tell an ordinary member that verifying unlocks moderation.
            StaffOnly = true
        };
    }
}
