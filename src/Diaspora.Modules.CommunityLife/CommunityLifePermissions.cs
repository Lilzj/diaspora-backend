namespace Diaspora.Modules.CommunityLife;

/// <summary>
/// The permissions the Community Life module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots,
/// see <see cref="CommunityLifePolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/CommunityLifePermissionContributor</c>, because the module is the only thing
/// that knows what each capability is worth.
/// </para>
/// <para>
/// <b><see cref="OrganizationCreate"/> is not a new name.</b> The host has declared
/// <c>organizations:create</c> provisionally since B2 and the mobile client already switches on
/// it. B12 claims the key unchanged and deletes the provisional entry — the same move B8, B9 and
/// B10 made, and for the same reason: two declarations of one permission fail startup.
/// </para>
/// <para>
/// <b>The split that matters: attending is Limited, organizing is Verified.</b> Browsing events,
/// reading an organization page, RSVPing to a public event and reporting a scam are how somebody
/// new to a city finds their people, and gating them behind identity proof would defeat the point
/// of the module. Running an organization, publishing an event and taking attendance each put
/// other members in a room with a stranger, which is exactly the progression B2.0.1 gates on
/// proven identity.
/// </para>
/// <para>
/// Staff-only capabilities are role-scoped, not tier-scoped, per the access-tier semantics: a tier
/// measures progression, a role grants privilege.
/// </para>
/// </summary>
public static class CommunityLifePermissions
{
    // ── Organizations ───────────────────────────────────────────

    /// <summary>
    /// The key the host has held provisionally since B2. Claimed here unchanged, because the
    /// mobile client already renders a gate against this exact string.
    /// </summary>
    public const string OrganizationCreate = "organizations:create";

    public const string OrganizationEditOwn = "community-life:organization:edit:own";
    public const string OrganizationEditAny = "community-life:organization:edit:any";
    public const string OrganizationPublish = "community-life:organization:publish";
    public const string OrganizationArchive = "community-life:organization:archive";
    public const string OrganizationRestore = "community-life:organization:restore";
    public const string OrganizationSubmitVerification =
        "community-life:organization:verify:submit";
    public const string OrganizationFeature = "community-life:organization:feature";
    public const string OrganizationManageMedia = "community-life:organization:media:manage";

    // ── Membership ──────────────────────────────────────────────

    public const string MembershipJoin = "community-life:membership:join";
    public const string MembershipManage = "community-life:membership:manage";
    public const string MembershipApprove = "community-life:membership:approve";
    public const string VolunteerManage = "community-life:volunteer:manage";

    // ── Events ──────────────────────────────────────────────────

    public const string EventCreate = "community-life:event:create";
    public const string EventEditOwn = "community-life:event:edit:own";
    public const string EventEditAny = "community-life:event:edit:any";
    public const string EventDeleteOwn = "community-life:event:delete:own";
    public const string EventPublish = "community-life:event:publish";
    public const string EventCancel = "community-life:event:cancel";
    public const string EventArchive = "community-life:event:archive";
    public const string EventRestore = "community-life:event:restore";
    public const string EventFeature = "community-life:event:feature";

    // ── Attendance ──────────────────────────────────────────────

    public const string Rsvp = "community-life:rsvp";
    public const string CheckInManage = "community-life:checkin:manage";

    // ── Shared ──────────────────────────────────────────────────

    public const string Report = "community-life:report";
    public const string ViewAnalytics = "community-life:analytics:view";

    // ── Staff ───────────────────────────────────────────────────

    public const string Moderate = "community-life:moderate";
    public const string VerifyOrganization = "community-life:organization:verify";
    public const string ManageCatalogue = "community-life:catalogue:manage";
}
