using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.CommunityProgramme.Authorization;

/// <summary>
/// What a caller must hold, and at which tier.
/// <para>
/// <b>Taking part is <see cref="AccessTier.Limited"/>; running something is
/// <see cref="AccessTier.Verified"/>.</b> Applying for a membership, offering to volunteer and voting
/// in a poll are how somebody new to a city finds their people, and gating them behind identity proof
/// would make the first useful thing they can do the thing they cannot. Running a programme, deciding
/// an application and publishing an announcement each put the caller in authority over other members,
/// which is the progression B2.0.1 gates on proven identity.
/// </para>
/// <para>
/// <b>Every one of these is a necessary condition and never a sufficient one.</b> The permission says
/// the caller may run a programme somewhere; whether they may run <em>this</em> one is a capability in
/// that organization, asked separately on every request. A token cannot carry the second.
/// </para>
/// </summary>
public sealed class CommunityProgrammePermissionContributor : IPermissionContributor
{
    public string Module => CommunityProgrammeModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        yield return new PermissionDefinition(
            CommunityProgrammePermissions.Read,
            Module,
            "Read membership programmes, volunteer opportunities, polls and announcements.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.Participate,
            Module,
            "Apply for a membership, offer to volunteer, and vote in a poll.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.ManageProgramme,
            Module,
            "Run an organization's membership programme: tiers, decisions and the member roll.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.ManageVolunteers,
            Module,
            "Run an organization's volunteering: opportunities, decisions, assignments and hours.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.ManagePolls,
            Module,
            "Run polls: creation, closing and publishing results.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.ProposeCommunity,
            Module,
            "Found a community, becoming its owner.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.PublishAnnouncements,
            Module,
            "Publish an announcement to a scope.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityProgrammePermissions.ViewAnalytics,
            Module,
            "Read an organization's programme analytics.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

/// <summary>
/// The module's feature gates.
/// <para>
/// Four rather than one, so a deployment launching with events and no paid memberships can turn
/// <see cref="CommunityProgrammeFeatures.Memberships"/> off and leave the polls on. One gate for the
/// whole module would make each of those an all-or-nothing decision.
/// </para>
/// </summary>
public sealed class CommunityProgrammeFeatureContributor : IFeatureContributor
{
    public string Module => CommunityProgrammeModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            CommunityProgrammeFeatures.Memberships,
            Module,
            "Join a community's membership programme, and manage one.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityProgrammeFeatures.Volunteering,
            Module,
            "Volunteer for community work, and run a volunteering programme.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityProgrammeFeatures.Polls,
            Module,
            "Vote in community polls, and run them.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            CommunityProgrammeFeatures.Announcements,
            Module,
            "Read community announcements, and publish them.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}

public sealed class CommunityProgrammePolicyContributor : IPolicyContributor
{
    public string Module => CommunityProgrammeModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.Read, Module, CommunityProgrammePermissions.Read);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.Participate,
            Module,
            CommunityProgrammePermissions.Participate);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.ManageProgramme,
            Module,
            CommunityProgrammePermissions.ManageProgramme);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.ManageVolunteers,
            Module,
            CommunityProgrammePermissions.ManageVolunteers);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.ManagePolls,
            Module,
            CommunityProgrammePermissions.ManagePolls);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.PublishAnnouncements,
            Module,
            CommunityProgrammePermissions.PublishAnnouncements);

        yield return PolicyDefinition.ForPermission(
            CommunityProgrammePolicies.ViewAnalytics,
            Module,
            CommunityProgrammePermissions.ViewAnalytics);
    }
}
