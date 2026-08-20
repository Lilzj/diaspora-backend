using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Policies;

/// <summary>
/// Which moves a notification may make, in one place.
/// <para>
/// <b>The lifecycle is not linear and pretending it is causes the two defects this table exists to
/// prevent.</b> A member can open a message that was never delivered by any channel — they opened
/// the app and scrolled. And a message can be archived from any live state, including one that was
/// never read. A strict ladder would refuse both, and both are the normal case.
/// </para>
/// <para>
/// What it does refuse is going backwards. An opened message never becomes merely delivered; an
/// expired one never returns to the queue. Restore moves an archived message back to whatever it
/// was before, which is why <see cref="RestoreTargetFor"/> exists rather than a single "restored"
/// state that would lose whether the member had read it.
/// </para>
/// </summary>
public static class NotificationStatusTransitions
{
    /// <summary>States a message can still be acted on from.</summary>
    public static bool IsLive(NotificationStatus status) =>
        status is NotificationStatus.Created
            or NotificationStatus.Queued
            or NotificationStatus.Delivered
            or NotificationStatus.Opened
            or NotificationStatus.Clicked;

    /// <summary>States nothing further happens from.</summary>
    public static bool IsTerminal(NotificationStatus status) =>
        status is NotificationStatus.Expired or NotificationStatus.Failed;

    /// <summary>Whether the message counts against the unread badge.</summary>
    public static bool CountsAsUnread(NotificationStatus status) =>
        status is NotificationStatus.Created
            or NotificationStatus.Queued
            or NotificationStatus.Delivered;

    public static bool CanTransition(NotificationStatus from, NotificationStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return to switch
        {
            NotificationStatus.Queued => from == NotificationStatus.Created,

            NotificationStatus.Delivered =>
                from is NotificationStatus.Created or NotificationStatus.Queued,

            // Reachable from Created: the member opened the app before any channel ran.
            NotificationStatus.Opened => IsLive(from) && from < NotificationStatus.Opened,

            NotificationStatus.Clicked => IsLive(from) && from < NotificationStatus.Clicked,

            NotificationStatus.Dismissed => IsLive(from),

            NotificationStatus.Archived => IsLive(from) || from == NotificationStatus.Dismissed,

            NotificationStatus.Expired => !IsTerminal(from),

            NotificationStatus.Failed => from is NotificationStatus.Created or NotificationStatus.Queued,

            _ => false
        };
    }

    /// <summary>
    /// What an archived message becomes when the member restores it.
    /// <para>
    /// The state it held before archiving, carried on the aggregate — because collapsing every
    /// restore to "delivered" would mark read messages unread and put a badge back on the client
    /// for something the member has already dealt with.
    /// </para>
    /// </summary>
    public static NotificationStatus RestoreTargetFor(NotificationStatus? previous) =>
        previous is { } value && IsLive(value) ? value : NotificationStatus.Delivered;
}
