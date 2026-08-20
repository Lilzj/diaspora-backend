using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The event lifecycle, as a machine.
/// <para>
/// <b><see cref="EventStatus.Cancelled"/> is reachable from everywhere and leads nowhere.</b> An
/// event can be called off at any point up to and including while it is running — a venue floods,
/// a speaker does not arrive — and once it is called off it stays called off. An organizer who
/// wants to try again runs a new event, because everybody who withdrew from the cancelled one
/// would otherwise be silently re-invited to it.
/// </para>
/// <para>
/// <b><see cref="EventStatus.Completed"/> leads only to Archived</b>, and never back to Published.
/// Republishing a finished event would put a past date at the top of a discovery feed, and worse,
/// would reopen RSVPs on something nobody can attend.
/// </para>
/// </summary>
public static class EventStatusTransitions
{
    private static readonly Dictionary<EventStatus, EventStatus[]> Allowed = new()
    {
        [EventStatus.Draft] =
        [
            EventStatus.Published,
            EventStatus.Cancelled,
            EventStatus.Archived
        ],
        [EventStatus.Published] =
        [
            EventStatus.Draft,
            EventStatus.InProgress,
            EventStatus.Completed,
            EventStatus.Cancelled,
            EventStatus.Archived
        ],
        [EventStatus.InProgress] =
        [
            EventStatus.Completed,
            EventStatus.Cancelled
        ],
        [EventStatus.Completed] = [EventStatus.Archived],
        [EventStatus.Cancelled] = [EventStatus.Archived],
        [EventStatus.Archived] = [EventStatus.Draft]
    };

    public static bool CanTransition(EventStatus from, EventStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Whether the event is reachable by anybody but its own organizers.</summary>
    public static bool IsDiscoverable(EventStatus status) =>
        status is EventStatus.Published or EventStatus.InProgress;

    /// <summary>
    /// Whether the lifecycle permits an RSVP. Not the whole answer — the deadline, the capacity
    /// and the visibility all have a say — but the first thing asked.
    /// </summary>
    public static bool AcceptsRsvp(EventStatus status) =>
        status is EventStatus.Published or EventStatus.InProgress;

    /// <summary>Whether attendance may be taken.</summary>
    public static bool AcceptsCheckIn(EventStatus status) =>
        status is EventStatus.Published or EventStatus.InProgress or EventStatus.Completed;

    /// <summary>Whether the content may still be edited.</summary>
    public static bool IsEditable(EventStatus status) =>
        status is EventStatus.Draft or EventStatus.Published or EventStatus.InProgress;
}
