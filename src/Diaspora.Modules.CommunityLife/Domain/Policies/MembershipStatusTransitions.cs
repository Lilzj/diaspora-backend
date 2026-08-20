using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Memberships;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The membership lifecycle, as a machine.
/// <para>
/// <b><see cref="MembershipStatus.Removed"/> leads only back to Active, and only an administrator
/// can make that move.</b> That asymmetry is the whole reason Removed and
/// <see cref="MembershipStatus.Left"/> are separate states: somebody who walked away can rejoin
/// themselves, and somebody who was removed cannot.
/// </para>
/// </summary>
public static class MembershipStatusTransitions
{
    private static readonly Dictionary<MembershipStatus, MembershipStatus[]> Allowed = new()
    {
        [MembershipStatus.Requested] =
        [
            MembershipStatus.Active,
            MembershipStatus.Rejected,
            MembershipStatus.Left
        ],
        [MembershipStatus.Invited] =
        [
            MembershipStatus.Active,
            MembershipStatus.Rejected,
            MembershipStatus.Left
        ],
        [MembershipStatus.Active] =
        [
            MembershipStatus.Left,
            MembershipStatus.Removed,
            MembershipStatus.Lapsed
        ],

        // A refusal is not permanent: asking again is allowed, and the history shows both attempts.
        [MembershipStatus.Rejected] =
        [
            MembershipStatus.Requested,
            MembershipStatus.Invited
        ],
        [MembershipStatus.Left] =
        [
            MembershipStatus.Requested,
            MembershipStatus.Invited,
            MembershipStatus.Active
        ],

        // Only an administrator reaches this arrow. The handler enforces that; the machine only
        // says the shape is legal.
        [MembershipStatus.Removed] = [MembershipStatus.Active],
        [MembershipStatus.Lapsed] =
        [
            MembershipStatus.Active,
            MembershipStatus.Left,
            MembershipStatus.Removed
        ]
    };

    public static bool CanTransition(MembershipStatus from, MembershipStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Whether the member is currently in the organization.</summary>
    public static bool IsCurrent(MembershipStatus status) => status == MembershipStatus.Active;

    /// <summary>Whether the membership is waiting on somebody to decide.</summary>
    public static bool IsPending(MembershipStatus status) =>
        status is MembershipStatus.Requested or MembershipStatus.Invited;

    /// <summary>Whether the member may be counted toward the organization's size.</summary>
    public static bool CountsTowardSize(MembershipStatus status) =>
        status == MembershipStatus.Active;

    /// <summary>
    /// How a membership moving between statuses or roles shifts the organization's counters.
    /// <para>
    /// <b>The role counters are "at least", not "exactly".</b> An administrator can take attendance,
    /// so they are counted among the volunteers as well — which is what makes the number answer the
    /// question a dashboard is actually asking ("how many people here can work the door"). It also
    /// matches <c>CountByRoleAsync</c>, the recount a sweep repairs from, so a delta and a
    /// reconciliation cannot disagree.
    /// </para>
    /// <para>
    /// <b>A null <paramref name="fromStatus"/> means a membership that did not exist.</b> Note that
    /// a newly opened membership raises its status event with the previous status <em>equal</em> to
    /// the current one — a caller that passed that through unchanged would compute a zero movement
    /// and never count the first member of an organization.
    /// </para>
    /// </summary>
    public static MembershipMovement Movement(
        MembershipStatus? fromStatus,
        MembershipRole? fromRole,
        MembershipStatus toStatus,
        MembershipRole toRole)
    {
        if (fromStatus == toStatus && fromRole == toRole)
        {
            return MembershipMovement.None;
        }

        return new MembershipMovement(
            Delta(static (status, _) => IsCurrent(status)),
            Delta(static (status, _) => IsPending(status)),
            Delta(static (status, role) =>
                IsCurrent(status) && role >= MembershipRole.Administrator),
            Delta(static (status, role) =>
                IsCurrent(status) && role >= MembershipRole.Volunteer));

        int Delta(Func<MembershipStatus, MembershipRole, bool> isCounted) =>
            (isCounted(toStatus, toRole) ? 1 : 0)
            - (fromStatus is { } previousStatus
                && isCounted(previousStatus, fromRole ?? toRole) ? 1 : 0);
    }
}
