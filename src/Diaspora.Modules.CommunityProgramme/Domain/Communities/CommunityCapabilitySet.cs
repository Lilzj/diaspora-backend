namespace Diaspora.Modules.CommunityProgramme.Domain.Communities;

/// <summary>
/// What one caller may do in one community — FRONTEND-PHASE13-GAP-006.
/// <para>
/// <b>The client evaluates none of this, and has no code path that could.</b> §62 forbids trusting
/// frontend community state for roles, moderation or content access; the register's own words are
/// that deriving a capability from a role "would show a moderation queue to somebody the server will
/// refuse — which is worse than showing nothing". So the derivation lives here, once, and the wire
/// carries eleven booleans.
/// </para>
/// <para>
/// <b>A capability nobody was granted is false, not absent.</b> The client renders a missing
/// capability as false anyway, but sending all eleven means a caller can tell "you cannot" from "the
/// server did not say", and an all-false set is a complete answer rather than a refusal — which is
/// what the register asks for in place of a 403 for a non-member.
/// </para>
/// <para>
/// <b>Staff bypass covers moderation and stops at the member roll.</b> A platform moderator may
/// review reports in a community they do not belong to, because moderation cannot require belonging
/// to everything it touches. Reading who belongs to a named community — a faith community, a
/// support group, a political association — is a different act, and it should take a deliberate
/// support tool rather than fall out of a role check. B9, B10, B11 and B12 all draw the same line,
/// and so does this module's <c>CommunityProgrammeAccessService</c>.
/// </para>
/// </summary>
public sealed record CommunityCapabilitySet
{
    private CommunityCapabilitySet() { }

    public required IReadOnlyList<string> Roles { get; init; }

    /// <summary>Read the member list. No staff bypass — see the type summary.</summary>
    public required bool CanViewMembers { get; init; }

    public required bool CanViewPrivateStatistics { get; init; }

    public required bool CanPostToCommunity { get; init; }

    public required bool CanModerateContent { get; init; }

    public required bool CanReviewReports { get; init; }

    public required bool CanPublishNotice { get; init; }

    public required bool CanManageRules { get; init; }

    public required bool CanManageRoles { get; init; }

    /// <summary>
    /// Appoint or remove administrators. The Owner only.
    /// <para>
    /// Separate from <see cref="CanManageRoles"/> because the two differ and the client must not
    /// work out which from <see cref="Roles"/> — it evaluates none of this. An administrator who
    /// could create administrators could hand the community to anybody, and one who could remove
    /// them could remove the person who founded it.
    /// </para>
    /// </summary>
    public required bool CanAppointAdministrators { get; init; }

    public required bool CanViewModerationQueue { get; init; }

    public required bool CanViewAmbassadorDirectory { get; init; }

    public required bool CanManageInitiatives { get; init; }

    /// <summary>
    /// Everything off. The answer for a stranger, and for every anonymous caller.
    /// </summary>
    public static CommunityCapabilitySet None { get; } = new()
    {
        Roles = [],
        CanViewMembers = false,
        CanViewPrivateStatistics = false,
        CanPostToCommunity = false,
        CanModerateContent = false,
        CanReviewReports = false,
        CanPublishNotice = false,
        CanManageRules = false,
        CanManageRoles = false,
        CanAppointAdministrators = false,
        CanViewModerationQueue = false,
        CanViewAmbassadorDirectory = false,
        CanManageInitiatives = false
    };

    /// <summary>
    /// Derives the set from the caller's standing and the community's state.
    /// <para>
    /// <b>An archived community grants nothing but reading.</b> Posting to, moderating or running a
    /// community that has closed for good is not a thing anybody may do, whatever role they hold, so
    /// the lifecycle is checked before the roles rather than after.
    /// </para>
    /// <para>
    /// <b>A suspended community keeps its moderation capabilities and loses its posting one.</b>
    /// Suspension is reversible and usually exists precisely so somebody can review what happened;
    /// taking the moderation queue away from the people reviewing it would be the wrong way round.
    /// </para>
    /// </summary>
    public static CommunityCapabilitySet For(
        Community? community, CommunityMembership? membership, bool isStaff = false)
    {
        if (community is null)
        {
            return None;
        }

        var roles = membership?.Roles ?? [];

        var isMember = membership?.IsCurrentMember == true;
        var isArchived = community.Status == CommunityStatus.Archived;
        var isLive = community.Status == CommunityStatus.Active;

        // The Owner always administers. Granted both roles on founding, and this reads either so a
        // row that somehow lost its Administrator entry still cannot lock out the person who owns
        // the community — the refusal in `RevokeRole` is the first line, and this is the second.
        var owns = Holds(roles, CommunityMembershipRole.Owner);
        var administers = owns || Holds(roles, CommunityMembershipRole.Administrator);
        var moderates = administers || Holds(roles, CommunityMembershipRole.Moderator);
        var represents = administers || Holds(roles, CommunityMembershipRole.Ambassador);

        // Staff moderate anywhere, including a community they do not belong to. They do not read
        // the member roll — see the type summary.
        var canModerate = (moderates || isStaff) && !isArchived;

        return new CommunityCapabilitySet
        {
            Roles = [.. roles],

            CanViewMembers = isMember,
            CanViewAmbassadorDirectory = isMember,

            // Only in a live community. A suspended one is stopped, and an archived one is closed.
            CanPostToCommunity = isMember && isLive,

            CanModerateContent = canModerate,
            CanReviewReports = canModerate,
            CanViewModerationQueue = canModerate,

            CanViewPrivateStatistics = (moderates || isStaff) && !isArchived,

            CanPublishNotice = administers && !isArchived,
            CanManageRules = administers && !isArchived,
            CanManageRoles = administers && !isArchived,
            CanAppointAdministrators = owns && !isArchived,
            CanManageInitiatives = represents && !isArchived
        };
    }

    private static bool Holds(IReadOnlyCollection<string> roles, CommunityMembershipRole role) =>
        roles.Contains(role.ToString(), StringComparer.Ordinal);
}
