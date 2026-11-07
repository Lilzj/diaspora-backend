using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.CommunityLife.Authorization;

/// <summary>
/// Declares the permissions the Community Life module owns.
/// <para>
/// <b>One product judgement runs through the whole band: attending is Limited, organizing is
/// Verified.</b> Browsing events, reading an organization page, responding to a public event and
/// reporting a scam are how somebody who has just landed in a city finds their people, and gating
/// them behind identity proof would defeat the point of the module. Running an organization,
/// publishing an event and taking attendance each put other members in a room with a stranger.
/// </para>
/// <para>
/// <b><c>organizations:create</c> is not a new key.</b> The host has declared it provisionally since
/// B2 at the Verified floor and the mobile client already switches on it. B12 claims it unchanged
/// and deletes the provisional entry in the same change — B8, B9 and B10 each recorded what happens
/// otherwise: the catalogue refuses two modules declaring one permission, and the startup failure
/// takes down every request that resolves it.
/// </para>
/// <para>
/// <b>The restriction exemptions are where this module's values are most visible.</b> A restricted
/// member may still cancel their own event, withdraw their own response, leave an organization and
/// report a fraud. Each of those, blocked, would leave somebody else waiting on a person the
/// platform has silenced — a hall booked for an event that will not happen, or a place held on a
/// full event by somebody who is not coming — which is the restriction working against the very
/// people it exists to protect.
/// </para>
/// <para>
/// The kernel constructs this type before the container exists, so it has a public parameterless
/// constructor and no dependencies.
/// </para>
/// </summary>
public sealed class CommunityLifePermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => CommunityLifeModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        foreach (var permission in OrganizationPermissions())
        {
            yield return permission;
        }

        foreach (var permission in MembershipPermissions())
        {
            yield return permission;
        }

        foreach (var permission in EventPermissions())
        {
            yield return permission;
        }

        foreach (var permission in AttendancePermissions())
        {
            yield return permission;
        }

        foreach (var permission in SharedAndStaffPermissions())
        {
            yield return permission;
        }
    }

    private static IEnumerable<PermissionDefinition> OrganizationPermissions()
    {
        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationCreate, CommunityLifeModuleMetadata.Name,
            "Create an organization.")
        {
            // The floor the host has held since B2, kept exactly. An organization page is an
            // institution's public face and members will trust it.
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationEditOwn, CommunityLifeModuleMetadata.Name,
            "Edit an organization you administer.")
        {
            MinimumTier = AccessTier.Verified,

            // Correcting a live page is never the thing to withhold: wrong opening hours or a wrong
            // address waste members' journeys, not the restricted administrator's.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationPublish, CommunityLifeModuleMetadata.Name,
            "Publish an organization.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationArchive, CommunityLifeModuleMetadata.Name,
            "Take an organization off the directory.")
        {
            MinimumTier = AccessTier.Verified,

            // Withdrawing a page for a body that has wound up must never be blocked. Leaving it up
            // earns the former administrators enquiries they cannot answer.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationRestore, CommunityLifeModuleMetadata.Name,
            "Restore an archived organization.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationSubmitVerification,
            CommunityLifeModuleMetadata.Name,
            "Ask Trust to verify an organization.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationFeature, CommunityLifeModuleMetadata.Name,
            "Feature or unfeature any organization.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.OrganizationManageMedia, CommunityLifeModuleMetadata.Name,
            "Manage an organization's media.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };
    }

    private static IEnumerable<PermissionDefinition> MembershipPermissions()
    {
        yield return new PermissionDefinition(
            CommunityLifePermissions.MembershipJoin, CommunityLifeModuleMetadata.Name,
            "Join an organization.")
        {
            // Limited, deliberately. Belonging somewhere is what a newcomer needs first, and the
            // organization's own policy decides the rest.
            MinimumTier = AccessTier.Limited
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.MembershipManage, CommunityLifeModuleMetadata.Name,
            "Manage an organization's members and their roles.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.MembershipApprove, CommunityLifeModuleMetadata.Name,
            "Approve or refuse membership requests.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.VolunteerManage, CommunityLifeModuleMetadata.Name,
            "Manage an organization's volunteers.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> EventPermissions()
    {
        yield return new PermissionDefinition(
            CommunityLifePermissions.EventCreate, CommunityLifeModuleMetadata.Name,
            "Create an event.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventEditOwn, CommunityLifeModuleMetadata.Name,
            "Edit an event you organize.")
        {
            MinimumTier = AccessTier.Verified,

            // A wrong time or a wrong venue on a live event wastes every attendee's evening.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventDeleteOwn, CommunityLifeModuleMetadata.Name,
            "Delete an event you organize.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventPublish, CommunityLifeModuleMetadata.Name,
            "Publish an event.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventCancel, CommunityLifeModuleMetadata.Name,
            "Cancel an event.")
        {
            MinimumTier = AccessTier.Verified,

            // The sharpest exemption in the module. An organizer who cannot cancel leaves people
            // travelling to a hall that will be locked.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventArchive, CommunityLifeModuleMetadata.Name,
            "Archive an event.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventRestore, CommunityLifeModuleMetadata.Name,
            "Restore an archived or deleted event.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.EventFeature, CommunityLifeModuleMetadata.Name,
            "Feature or unfeature any event.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };
    }

    private static IEnumerable<PermissionDefinition> AttendancePermissions()
    {
        yield return new PermissionDefinition(
            CommunityLifePermissions.Rsvp, CommunityLifeModuleMetadata.Name,
            "Say whether you are coming to an event.")
        {
            // Limited, and this is the module's central access decision. An RSVP is how somebody new
            // to a country finds their people, and it costs nobody anything.
            MinimumTier = AccessTier.Limited,

            // Withdrawing must survive a restriction, or the restriction holds a place on a full
            // event for somebody who is not coming.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.CheckInManage, CommunityLifeModuleMetadata.Name,
            "Take attendance at an event.")
        {
            // Verified: taking attendance means standing at a door with a list of who is inside.
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> SharedAndStaffPermissions()
    {
        yield return new PermissionDefinition(
            CommunityLifePermissions.Report, CommunityLifeModuleMetadata.Name,
            "Report an event or an organization.")
        {
            MinimumTier = AccessTier.Limited,

            // Reporting a fake charity collection must survive a restriction, or the restriction
            // silences the person best placed to flag it.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.ViewAnalytics, CommunityLifeModuleMetadata.Name,
            "Read the figures for your own events and organizations.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.Moderate, CommunityLifeModuleMetadata.Name,
            "Read the reports members filed about events and organizations. Staff act on one only by deciding its Trust moderation case.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.VerifyOrganization, CommunityLifeModuleMetadata.Name,
            "Record a verification decision about an organization.")
        {
            // Narrower than moderation. Vouching for an institution under the platform's name is a
            // different act from hiding a bad page, and Trust owns the decision regardless — this
            // permission only guards the endpoint that files the claim on somebody else's behalf.
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            CommunityLifePermissions.ManageCatalogue, CommunityLifeModuleMetadata.Name,
            "Manage the organization-type and event-category catalogues.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };
    }
}
