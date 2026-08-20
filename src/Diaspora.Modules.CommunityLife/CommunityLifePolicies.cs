namespace Diaspora.Modules.CommunityLife;

/// <summary>
/// The authorisation policy names the module's endpoints reference.
/// <para>
/// Dot-separated, mirroring <see cref="CommunityLifePermissions"/> one-for-one. The kernel
/// validates at startup that each policy names a permission that exists, so a typo fails the boot
/// rather than silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything scoped to a
/// particular organization or event.</b> <c>community-life.event.edit.own</c> says the caller may
/// edit events somewhere; whether they may edit <em>this</em> one is answered by the membership
/// role they hold in the organizing organization, and every such handler asks.
/// </para>
/// </summary>
public static class CommunityLifePolicies
{
    // ── Organizations ───────────────────────────────────────────

    public const string OrganizationCreate = "community-life.organization.create";
    public const string OrganizationEditOwn = "community-life.organization.edit.own";
    public const string OrganizationEditAny = "community-life.organization.edit.any";
    public const string OrganizationPublish = "community-life.organization.publish";
    public const string OrganizationArchive = "community-life.organization.archive";
    public const string OrganizationRestore = "community-life.organization.restore";
    public const string OrganizationSubmitVerification =
        "community-life.organization.verify.submit";
    public const string OrganizationFeature = "community-life.organization.feature";
    public const string OrganizationManageMedia = "community-life.organization.media.manage";

    // ── Membership ──────────────────────────────────────────────

    public const string MembershipJoin = "community-life.membership.join";
    public const string MembershipManage = "community-life.membership.manage";
    public const string MembershipApprove = "community-life.membership.approve";
    public const string VolunteerManage = "community-life.volunteer.manage";

    // ── Events ──────────────────────────────────────────────────

    public const string EventCreate = "community-life.event.create";
    public const string EventEditOwn = "community-life.event.edit.own";
    public const string EventEditAny = "community-life.event.edit.any";
    public const string EventDeleteOwn = "community-life.event.delete.own";
    public const string EventPublish = "community-life.event.publish";
    public const string EventCancel = "community-life.event.cancel";
    public const string EventArchive = "community-life.event.archive";
    public const string EventRestore = "community-life.event.restore";
    public const string EventFeature = "community-life.event.feature";

    // ── Attendance ──────────────────────────────────────────────

    public const string Rsvp = "community-life.rsvp";
    public const string CheckInManage = "community-life.checkin.manage";

    // ── Shared ──────────────────────────────────────────────────

    public const string Report = "community-life.report";
    public const string ViewAnalytics = "community-life.analytics.view";

    // ── Staff ───────────────────────────────────────────────────

    public const string Moderate = "community-life.moderate";
    public const string VerifyOrganization = "community-life.organization.verify";
    public const string ManageCatalogue = "community-life.catalogue.manage";
}
