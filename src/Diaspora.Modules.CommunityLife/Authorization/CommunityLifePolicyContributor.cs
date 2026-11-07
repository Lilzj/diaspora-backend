using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.CommunityLife.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="CommunityLifePermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything scoped to a particular
/// organization or event.</b> <c>community-life.event.publish</c> says the caller may publish events
/// somewhere; whether they may publish <em>this</em> one is answered by the membership role they
/// hold in the organizing organization, and every such handler asks. Reading the policy list as the
/// authorisation model would understate it by half.
/// </para>
/// </summary>
public sealed class CommunityLifePolicyContributor : IPolicyContributor
{
    public string Module => CommunityLifeModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        // Organizations
        yield return Policy(
            CommunityLifePolicies.OrganizationCreate,
            CommunityLifePermissions.OrganizationCreate);
        yield return Policy(
            CommunityLifePolicies.OrganizationEditOwn,
            CommunityLifePermissions.OrganizationEditOwn);
        yield return Policy(
            CommunityLifePolicies.OrganizationPublish,
            CommunityLifePermissions.OrganizationPublish);
        yield return Policy(
            CommunityLifePolicies.OrganizationArchive,
            CommunityLifePermissions.OrganizationArchive);
        yield return Policy(
            CommunityLifePolicies.OrganizationRestore,
            CommunityLifePermissions.OrganizationRestore);
        yield return Policy(
            CommunityLifePolicies.OrganizationSubmitVerification,
            CommunityLifePermissions.OrganizationSubmitVerification);
        yield return Policy(
            CommunityLifePolicies.OrganizationFeature,
            CommunityLifePermissions.OrganizationFeature);
        yield return Policy(
            CommunityLifePolicies.OrganizationManageMedia,
            CommunityLifePermissions.OrganizationManageMedia);

        // Membership
        yield return Policy(
            CommunityLifePolicies.MembershipJoin, CommunityLifePermissions.MembershipJoin);
        yield return Policy(
            CommunityLifePolicies.MembershipManage, CommunityLifePermissions.MembershipManage);
        yield return Policy(
            CommunityLifePolicies.MembershipApprove, CommunityLifePermissions.MembershipApprove);
        yield return Policy(
            CommunityLifePolicies.VolunteerManage, CommunityLifePermissions.VolunteerManage);

        // Events
        yield return Policy(
            CommunityLifePolicies.EventCreate, CommunityLifePermissions.EventCreate);
        yield return Policy(
            CommunityLifePolicies.EventEditOwn, CommunityLifePermissions.EventEditOwn);
        yield return Policy(
            CommunityLifePolicies.EventDeleteOwn, CommunityLifePermissions.EventDeleteOwn);
        yield return Policy(
            CommunityLifePolicies.EventPublish, CommunityLifePermissions.EventPublish);
        yield return Policy(
            CommunityLifePolicies.EventCancel, CommunityLifePermissions.EventCancel);
        yield return Policy(
            CommunityLifePolicies.EventArchive, CommunityLifePermissions.EventArchive);
        yield return Policy(
            CommunityLifePolicies.EventRestore, CommunityLifePermissions.EventRestore);
        yield return Policy(
            CommunityLifePolicies.EventFeature, CommunityLifePermissions.EventFeature);

        // Attendance
        yield return Policy(CommunityLifePolicies.Rsvp, CommunityLifePermissions.Rsvp);
        yield return Policy(
            CommunityLifePolicies.CheckInManage, CommunityLifePermissions.CheckInManage);

        // Shared
        yield return Policy(CommunityLifePolicies.Report, CommunityLifePermissions.Report);
        yield return Policy(
            CommunityLifePolicies.ViewAnalytics, CommunityLifePermissions.ViewAnalytics);

        // Staff
        yield return Policy(CommunityLifePolicies.Moderate, CommunityLifePermissions.Moderate);
        yield return Policy(
            CommunityLifePolicies.VerifyOrganization,
            CommunityLifePermissions.VerifyOrganization);
        yield return Policy(
            CommunityLifePolicies.ManageCatalogue, CommunityLifePermissions.ManageCatalogue);
    }

    private static PolicyDefinition Policy(string name, string permission) =>
        PolicyDefinition.ForPermission(name, CommunityLifeModuleMetadata.Name, permission);
}
