using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The RSVP journey, as a machine.
/// <para>
/// <b>The brief draws this as a straight line and it is not one.</b> Somebody can go from
/// Interested straight to Cancelled without ever saying they were coming; somebody waitlisted is
/// promoted to Going when a place frees; and somebody who checked in cannot then become a no-show,
/// because the door already saw them.
/// </para>
/// <para>
/// <b><see cref="RsvpStatus.NoShow"/> is reachable only from <see cref="RsvpStatus.Going"/></b>,
/// which is the rule that keeps the no-show rate honest. Somebody who was merely Interested and
/// did not come has not let anybody down, and counting them would make every popular event look
/// badly attended.
/// </para>
/// </summary>
public static class RsvpStatusTransitions
{
    private static readonly Dictionary<RsvpStatus, RsvpStatus[]> Allowed = new()
    {
        [RsvpStatus.Interested] =
        [
            RsvpStatus.Going,
            RsvpStatus.Waitlisted,
            RsvpStatus.Cancelled
        ],
        [RsvpStatus.Going] =
        [
            RsvpStatus.Interested,
            RsvpStatus.CheckedIn,
            RsvpStatus.Attended,
            RsvpStatus.Cancelled,
            RsvpStatus.NoShow
        ],
        [RsvpStatus.Waitlisted] =
        [
            RsvpStatus.Going,
            RsvpStatus.Interested,
            RsvpStatus.Cancelled
        ],
        [RsvpStatus.CheckedIn] = [RsvpStatus.Attended],

        // Terminal. An attendance that could be withdrawn is an attendance record nobody can rely
        // on, and the reputation signals downstream are computed from it.
        [RsvpStatus.Attended] = [],

        // Coming back after cancelling is a fresh commitment, and the history records both.
        [RsvpStatus.Cancelled] =
        [
            RsvpStatus.Interested,
            RsvpStatus.Going,
            RsvpStatus.Waitlisted
        ],

        // A no-show can be corrected — the door missed somebody — but only into an attendance.
        [RsvpStatus.NoShow] = [RsvpStatus.Attended]
    };

    public static bool CanTransition(RsvpStatus from, RsvpStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Whether the status holds a place against the event's capacity.</summary>
    public static bool HoldsCapacity(RsvpStatus status) =>
        status is RsvpStatus.Going or RsvpStatus.CheckedIn or RsvpStatus.Attended;

    /// <summary>
    /// Whether the status counts as somebody who came through the door.
    /// <para>
    /// A check-in does not release the place it occupies, which is why an attendance is in this
    /// bucket <em>and</em> in <see cref="HoldsCapacity"/>. Counting them apart would make an event's
    /// going count fall as the hall filled up.
    /// </para>
    /// </summary>
    public static bool IsPresent(RsvpStatus status) =>
        status is RsvpStatus.CheckedIn or RsvpStatus.Attended;

    /// <summary>Whether the journey is over.</summary>
    public static bool IsTerminal(RsvpStatus status) =>
        status is RsvpStatus.Attended or RsvpStatus.NoShow;

    /// <summary>
    /// How a response moving from one status to another shifts the event's four counters.
    /// <para>
    /// <b>Places, not responses.</b> "Me and three others" is how a family answers a naming
    /// ceremony, so every counter moves by the party size. Counting rows would have an organizer
    /// lay out a quarter of the chairs they need.
    /// </para>
    /// <para>
    /// <b>The buckets are the same four <c>SumPlacesAsync</c> recounts</b>, expressed once here so
    /// the settlement path and the repair path cannot disagree. Two hand-written copies of "which
    /// statuses hold a place" would drift the first time a status was added, and the symptom would
    /// be an organizer told there are places left when there are none.
    /// </para>
    /// <para>
    /// <paramref name="from"/> is null for a first response: the same arithmetic with nothing to
    /// take away.
    /// </para>
    /// </summary>
    public static RsvpPlaceMovement Movement(RsvpStatus? from, RsvpStatus to, int places)
    {
        if (places <= 0 || from == to)
        {
            return RsvpPlaceMovement.None;
        }

        return new RsvpPlaceMovement(
            Delta(HoldsCapacity),
            Delta(static status => status == RsvpStatus.Interested),
            Delta(static status => status == RsvpStatus.Waitlisted),
            Delta(IsPresent));

        int Delta(Func<RsvpStatus, bool> isInBucket) =>
            (isInBucket(to) ? places : 0)
            - (from is { } previous && isInBucket(previous) ? places : 0);
    }
}
