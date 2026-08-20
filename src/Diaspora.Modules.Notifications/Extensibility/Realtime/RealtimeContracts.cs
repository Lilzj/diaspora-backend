namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// What the server tells a client the moment its socket is accepted.
/// <para>
/// <b>The capability flags are the point.</b> A client that has to discover by trial that presence
/// is switched off in this deployment renders a grey dot forever and nobody knows why. Declaring it
/// on connect means one code path in the app handles "this deployment does not do typing
/// indicators" and it is the same path that handles an older server.
/// </para>
/// </summary>
/// <param name="ConnectionId">The transport's handle, echoed so a client can correlate its own logs.</param>
/// <param name="UserId">Who the server authenticated. A client comparing this to its own token catches a stale session.</param>
/// <param name="ServerTime">For clock-skew correction on relative timestamps.</param>
/// <param name="PresenceEnabled">Whether presence is tracked and broadcast at all.</param>
/// <param name="TypingEnabled">Whether typing signals are relayed.</param>
/// <param name="ReadReceiptsEnabled">Whether read receipts are relayed.</param>
/// <param name="DeliveryReceiptsEnabled">Whether delivery acknowledgements are relayed.</param>
/// <param name="ConversationRelayEnabled">
/// Whether conversation groups can be joined at all. False on every deployment until a module owns
/// conversations and registers an <see cref="IRealtimeGroupAuthorizer"/> — see that interface.
/// </param>
public sealed record RealtimeHandshake(
    string ConnectionId,
    Guid UserId,
    DateTimeOffset ServerTime,
    bool PresenceEnabled,
    bool TypingEnabled,
    bool ReadReceiptsEnabled,
    bool DeliveryReceiptsEnabled,
    bool ConversationRelayEnabled);

/// <summary>
/// A notification, as a connected client receives it.
/// <para>
/// <b>The same words the inbox holds, under the same names.</b> This is the live view of a row the
/// member can already read, so every field here is spelled exactly as
/// <c>NotificationResponse</c> spells it — <c>id</c> not <c>notificationId</c>, <c>targetRoute</c>
/// not <c>route</c>. A client that has been offline fetches the inbox and gets these fields back
/// inside a larger record, so one render path serves both and no mapper sits between them. Naming
/// them independently would have been the small, permanent friction that makes a client keep two.
/// </para>
/// <para>
/// <b>A subset, not a second richer channel.</b> Absent here and present on the REST shape:
/// <c>isRead</c>, <c>isArchived</c>, <c>isPinned</c>, <c>isStarred</c>, <c>status</c>,
/// <c>sourceModule</c>, <c>entityType</c>, <c>entityId</c> and <c>actions</c>. A notification that
/// has this instant arrived has known values for the first five, and the rest are inbox-screen
/// concerns a live banner does not render.
/// </para>
/// <para>
/// <b>No delivery identifiers, no template internals, no rule key.</b> Those are operational and
/// belong on the operator surface; a client that received them would be a client that could start
/// depending on them.
/// </para>
/// </summary>
/// <param name="Id">The inbox row. Named <c>Id</c>, not <c>NotificationId</c> — see the summary.</param>
/// <param name="Title">Rendered and localised.</param>
/// <param name="Body">Rendered and localised. Not truncated: a screen is not a lock screen.</param>
/// <param name="CategoryKey">The stable machine key. For grouping and for per-category settings.</param>
/// <param name="Priority">The rendered priority name — <c>Critical</c>, <c>High</c>, <c>Normal</c>, <c>Low</c>, <c>Silent</c>.</param>
/// <param name="CreatedAt">When the notification was created, not when it was relayed.</param>
/// <param name="TargetRoute">The client route to open, already substituted. Null when it opens nothing.</param>
/// <param name="ImageMediaId">A B5 asset identifier, never a URL. ADR-005.</param>
public sealed record RealtimeNotification(
    Guid Id,
    string Title,
    string Body,
    string CategoryKey,
    string Priority,
    DateTimeOffset CreatedAt,
    string? TargetRoute,
    Guid? ImageMediaId);

/// <summary>
/// The badge, pushed rather than polled.
/// <para>
/// Sent alongside every <see cref="RealtimeEvents.NotificationReceived"/> so a client never has to
/// re-count locally and never disagrees with the inbox screen. <see cref="Total"/> is the number the
/// tab badge renders; the per-category breakdown is what the filter chips render.
/// </para>
/// <para>
/// <b><c>total</c> and <c>byCategory</c> are spelled as <c>UnreadCountResponse</c> spells them</b>,
/// for the same reason the notification is: a client stores one badge, and two names for it is how a
/// client ends up with two.
/// </para>
/// </summary>
/// <param name="Total">Total unread, across every category.</param>
/// <param name="ByCategory">Unread per category key. Categories with none are omitted.</param>
/// <param name="At">When the count was taken.</param>
public sealed record InboxCounters(
    int Total,
    IReadOnlyDictionary<string, int> ByCategory,
    DateTimeOffset At);

/// <summary>
/// Somebody is typing, or has stopped.
/// <para>
/// <b>Relayed, never stored.</b> Typing is worthless a second after it is true, and a platform that
/// persisted it would be paying for a write per keystroke to answer a question nobody asks about the
/// past.
/// </para>
/// <para>
/// <see cref="EventType"/> carries the client's own vocabulary — <c>TypingStarted</c>,
/// <c>TypingUpdated</c>, <c>TypingEnded</c> — rather than a boolean, because a client that misses an
/// "ended" needs a repeated "started" to refresh its own timeout, and a boolean cannot say which
/// kind of true it is.
/// </para>
/// </summary>
/// <param name="EventType"><c>TypingStarted</c>, <c>TypingUpdated</c> or <c>TypingEnded</c>.</param>
/// <param name="ConversationId">Which conversation. Opaque to this module.</param>
/// <param name="UserId">Who is typing.</param>
/// <param name="Timestamp">Server time, so clients with skewed clocks expire it consistently.</param>
public sealed record TypingSignal(
    string EventType,
    Guid ConversationId,
    Guid UserId,
    DateTimeOffset Timestamp)
{
    public const string Started = "TypingStarted";

    public const string Updated = "TypingUpdated";

    public const string Ended = "TypingEnded";
}

/// <summary>
/// A member's availability changed.
/// <para>
/// <see cref="Status"/> is the lower-case wire spelling. <see cref="LastSeenAt"/> is populated only
/// when they are not online — see <see cref="PresenceSnapshot"/>.
/// </para>
/// </summary>
/// <param name="UserId">Who.</param>
/// <param name="Status"><c>online</c>, <c>away</c> or <c>offline</c>.</param>
/// <param name="LastSeenAt">When they were last connected. Null while connected.</param>
/// <param name="Timestamp">When the change was observed.</param>
public sealed record PresenceChanged(
    Guid UserId,
    string Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset Timestamp);

/// <summary>
/// A message was opened by its recipient.
/// <para>
/// <b>Relayed on behalf of whoever owns the message, and never interpreted here.</b> This module
/// does not know what <paramref name="MessageId"/> refers to and must not: a runtime that validated
/// message identifiers would be a runtime that owned messages.
/// </para>
/// </summary>
/// <param name="ConversationId">Which conversation.</param>
/// <param name="MessageId">Which message.</param>
/// <param name="ReadByUserId">Who read it.</param>
/// <param name="ReadAt">When, in server time.</param>
public sealed record ReadReceipt(
    Guid ConversationId,
    Guid MessageId,
    Guid ReadByUserId,
    DateTimeOffset ReadAt);

/// <summary>
/// A message reached a recipient's device, which is not the same as being read.
/// <para>
/// The distinction is the whole reason two receipt types exist: one tick means the server has it,
/// two means a device does, blue means a person did. Collapsing delivery into read would make a
/// client claim somebody has seen something they have not.
/// </para>
/// </summary>
/// <param name="ConversationId">Which conversation.</param>
/// <param name="MessageId">Which message.</param>
/// <param name="DeliveredToUserId">Whose device.</param>
/// <param name="DeliveredAt">When, in server time.</param>
public sealed record DeliveryReceipt(
    Guid ConversationId,
    Guid MessageId,
    Guid DeliveredToUserId,
    DateTimeOffset DeliveredAt);

/// <summary>
/// A record the member is watching changed, and reading it again would return something different.
/// <para>
/// <b>An invalidation, not a representation.</b> Nothing here is renderable and nothing here is
/// authoritative: there is no title, no amount, no status text and no name. A client that rendered
/// this would be rendering a claim it cannot verify, made by a transport that is allowed to drop
/// frames. It refetches the REST resource, which is the only thing that ever tells it what a record
/// says. That is also what makes the event safe to send to a channel — a member who may join
/// <c>event:{id}</c> is told the event changed, and what it now says is still governed by the read
/// authorisation on the resource itself.
/// </para>
/// <para>
/// <b>Three opaque strings and a moment, because the alternative is a client release per vertical.</b>
/// <see cref="ResourceType"/> and <see cref="ChangeKind"/> are the vocabulary a client switches on;
/// both are lower-case, dotted where they need structure, and appended to rather than repurposed —
/// a client that does not recognise a pair invalidates nothing and is correct to, because a screen it
/// does not have cannot be stale. See <c>RealtimeEvents.ResourceChanged</c> for why this is one event
/// rather than one method per domain.
/// </para>
/// <para>
/// <b><see cref="Reason"/> is for a client that must say <em>why</em> something vanished</b> — an
/// event cancelled by its organizer reads differently from one removed by a moderator, and a screen
/// that could not tell them apart would have to say nothing. It is a stable key, never prose, and
/// never carries a member's words: the reason a human typed is content, it is subject to moderation
/// and erasure, and a socket frame is the one place neither can reach it.
/// </para>
/// </summary>
/// <param name="ResourceType">What kind of record — <c>event</c>, <c>payment</c>, <c>organization</c>.</param>
/// <param name="ResourceId">Its identifier, as a string. Opaque to the runtime.</param>
/// <param name="ChangeKind">What happened — <c>updated</c>, <c>cancelled</c>, <c>rescheduled</c>.</param>
/// <param name="Reason">A stable key explaining the change, or null. Never free text.</param>
/// <param name="OccurredAt">When the fact happened, not when it was relayed.</param>
public sealed record ResourceChanged(
    string ResourceType,
    string ResourceId,
    string ChangeKind,
    string? Reason,
    DateTimeOffset OccurredAt)
{
    /// <summary>The vocabulary this platform ships. A client switches on these.</summary>
    public static class Kinds
    {
        public const string Created = "created";

        public const string Updated = "updated";

        public const string Cancelled = "cancelled";

        public const string Rescheduled = "rescheduled";

        public const string StatusChanged = "status.changed";

        public const string Removed = "removed";
    }
}
