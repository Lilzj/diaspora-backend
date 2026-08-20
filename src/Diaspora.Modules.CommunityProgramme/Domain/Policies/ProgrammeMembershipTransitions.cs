using Diaspora.Modules.CommunityProgramme.Domain.Enums;

namespace Diaspora.Modules.CommunityProgramme.Domain.Policies;

/// <summary>
/// The fourteen-state membership machine, as a table.
/// <para>
/// <b>The server's copy is the authority and the client's is a rendering hint.</b> The frontend
/// implements the same table to decide which buttons to draw; when the two disagree, the client has
/// gone stale and a 409 is the correct outcome — not something to be prevented by making the client
/// cleverer.
/// </para>
/// <para>
/// <b>Three states are never stored.</b> <see cref="ProgrammeMembershipStatus.NotAMember"/>,
/// <see cref="ProgrammeMembershipStatus.Eligible"/> and
/// <see cref="ProgrammeMembershipStatus.Ineligible"/> describe a caller with no row at all, and the
/// difference between the last two is the server's evaluation of the tier's requirements. They
/// appear on the programme read and nowhere in this table's targets.
/// </para>
/// <para>
/// <b><see cref="ProgrammeMembershipStatus.Expired"/> is terminal for the membership and re-entrant
/// for the member.</b> Rejoining creates a new record rather than reviving this one — which is what
/// keeps the history of the first membership readable, and what stops a member who was removed
/// re-entering through a door marked "renew".
/// </para>
/// </summary>
public static class ProgrammeMembershipTransitions
{
    private static readonly Dictionary<ProgrammeMembershipStatus, ProgrammeMembershipStatus[]> Allowed =
        new()
        {
            [ProgrammeMembershipStatus.Draft] =
            [
                ProgrammeMembershipStatus.Pending,
                ProgrammeMembershipStatus.Cancelled
            ],

            // An application on a programme that needs no approval goes straight to Approved and,
            // when nothing is owed, straight on to Active. Both hops are in the table because a
            // programme's approval setting can change between the application and the decision.
            [ProgrammeMembershipStatus.Pending] =
            [
                ProgrammeMembershipStatus.Approved,
                ProgrammeMembershipStatus.Rejected,
                ProgrammeMembershipStatus.Cancelled
            ],

            [ProgrammeMembershipStatus.Approved] =
            [
                ProgrammeMembershipStatus.PaymentPending,
                ProgrammeMembershipStatus.Active,
                ProgrammeMembershipStatus.Cancelled
            ],

            [ProgrammeMembershipStatus.PaymentPending] =
            [
                ProgrammeMembershipStatus.Active,
                ProgrammeMembershipStatus.PaymentFailed
            ],

            [ProgrammeMembershipStatus.PaymentFailed] =
            [
                ProgrammeMembershipStatus.PaymentPending,
                ProgrammeMembershipStatus.Cancelled
            ],

            [ProgrammeMembershipStatus.Active] =
            [
                ProgrammeMembershipStatus.RenewalDue,
                ProgrammeMembershipStatus.Suspended,
                ProgrammeMembershipStatus.Expired,
                ProgrammeMembershipStatus.Cancelled
            ],

            [ProgrammeMembershipStatus.RenewalDue] =
            [
                ProgrammeMembershipStatus.PaymentPending,
                ProgrammeMembershipStatus.Active,
                ProgrammeMembershipStatus.Expired,
                ProgrammeMembershipStatus.Suspended,
                ProgrammeMembershipStatus.Cancelled
            ],

            // Reinstating a suspended membership returns it to Active. It does not return it to
            // RenewalDue, even if the date has passed — the sweep will move it there on its next
            // pass, and doing it here would mean a reinstatement that silently expired.
            [ProgrammeMembershipStatus.Suspended] =
            [
                ProgrammeMembershipStatus.Active,
                ProgrammeMembershipStatus.Cancelled
            ],

            // ── The three re-entrant ends ───────────────────────────────────────────────────
            //
            // Terminal for the chapter, re-entrant for the member. Rejoining moves the same row back
            // to Pending or Approved rather than opening a second one, and the row's history keeps
            // both attempts.
            //
            // **The register describes this as "creates a new record", and the backend does it on
            // one row instead.** The reason is the unique index on (organization, member), which is
            // what makes "you already hold one of these" a database fact rather than a race between
            // two taps — and a second row would violate it. B12 made the same call for its own
            // membership edge and for the same reason; keeping the history on one row is also what
            // makes a readmission decision possible, because the administrator deciding can see that
            // this person was removed in March.
            //
            // What a client sees: the membership identifier is stable across a rejoin. Nothing in
            // the register's screens depends on it changing.
            [ProgrammeMembershipStatus.Expired] =
            [
                ProgrammeMembershipStatus.Pending,
                ProgrammeMembershipStatus.Approved
            ],

            [ProgrammeMembershipStatus.Rejected] =
            [
                ProgrammeMembershipStatus.Pending,
                ProgrammeMembershipStatus.Approved
            ],

            [ProgrammeMembershipStatus.Cancelled] =
            [
                ProgrammeMembershipStatus.Pending,
                ProgrammeMembershipStatus.Approved
            ]
        };

    public static bool CanTransition(
        ProgrammeMembershipStatus from, ProgrammeMembershipStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Whether the membership currently entitles its holder to the tier's benefits.
    /// <para>
    /// <b><see cref="ProgrammeMembershipStatus.RenewalDue"/> counts, and that is a product decision
    /// stated once here rather than twenty times across the screens.</b> Somebody one day past their
    /// renewal date has not lost their membership; the grace period is the organization's to set,
    /// and the state exists so a member inside it keeps what they paid for.
    /// </para>
    /// </summary>
    public static bool GrantsBenefits(ProgrammeMembershipStatus status) =>
        status is ProgrammeMembershipStatus.Active or ProgrammeMembershipStatus.RenewalDue;

    /// <summary>Whether the membership occupies a place in its tier's capacity.</summary>
    public static bool OccupiesCapacity(ProgrammeMembershipStatus status) =>
        status is ProgrammeMembershipStatus.Pending
            or ProgrammeMembershipStatus.Approved
            or ProgrammeMembershipStatus.PaymentPending
            or ProgrammeMembershipStatus.Active
            or ProgrammeMembershipStatus.RenewalDue
            or ProgrammeMembershipStatus.Suspended;

    /// <summary>Whether nothing further will happen to this membership without the member acting.</summary>
    public static bool IsTerminal(ProgrammeMembershipStatus status) =>
        status is ProgrammeMembershipStatus.Rejected
            or ProgrammeMembershipStatus.Cancelled
            or ProgrammeMembershipStatus.Expired;

    /// <summary>Whether the member may open a fresh application while holding this one.</summary>
    public static bool AllowsReapplication(ProgrammeMembershipStatus status) =>
        status is ProgrammeMembershipStatus.Rejected
            or ProgrammeMembershipStatus.Cancelled
            or ProgrammeMembershipStatus.Expired;
}
