using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.CommunityLife.Authorization;

/// <summary>
/// Declares the feature gates the Community Life module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and failing.
/// The split mirrors the permission band: browsing, responding and joining are open at
/// <see cref="AccessTier.Limited"/>; creating organizations and publishing events are
/// <see cref="AccessTier.Verified"/>; moderation is staff-only — a gate that told a member they were
/// merely locked out of the moderation queue would imply verifying unlocks it. It does not.
/// </para>
/// <para>
/// <b><c>organizations.create</c> is claimed from the provisional catalogue.</b> The host has
/// declared it since B2 at the Verified floor and the mobile client renders against it. It is
/// claimed here unchanged and the provisional entry is deleted in the same change — the feature
/// catalogue refuses two modules declaring one gate, and the resulting startup failure takes down
/// every request that resolves it, phone verification included. B8, B9 and B10 each recorded the
/// trap; this is the first phase since B7 to walk into it deliberately rather than to find nothing
/// to claim.
/// </para>
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class CommunityLifeFeatureContributor : IFeatureContributor
{
    public string Module => CommunityLifeModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            CommunityLifeFeatures.Browse, Module, "Browse events and organizations.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityLifeFeatures.Rsvp, Module, "Say whether you are coming to an event.")
        {
            // The module's central access decision. See the permission contributor.
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityLifeFeatures.Membership, Module, "Join an organization.")
        {
            MinimumTier = AccessTier.Limited
        };

        // The gate the host has held provisionally since B2, claimed unchanged. Deleting the
        // provisional entry is part of the same change; leaving both would fail startup.
        yield return new FeatureDefinition(
            CommunityLifeFeatures.OrganizationCreate, Module, "Create an organization.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityLifeFeatures.EventCreate, Module, "Publish an event.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityLifeFeatures.CheckIn, Module, "Take attendance at an event.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new FeatureDefinition(
            CommunityLifeFeatures.Moderate, Module, "Work the community moderation queue.")
        {
            MinimumTier = AccessTier.Limited,

            // Staff-only rather than tier-gated, and the distinction is B2.0.1's: a tier measures
            // how far a member has progressed, a role grants a privilege. A gate that merely raised
            // the tier would tell an ordinary member that verifying unlocks moderation — and here
            // that would mean telling them verification unlocks reading the reporter's identity on
            // every complaint the platform holds.
            StaffOnly = true
        };
    }
}
