namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// The names a client subscribes to. This is a published contract.
/// <para>
/// <b>Renaming one of these breaks every shipped mobile build, and no compiler will say so.</b> A
/// hub method name is matched by string at the client, so the usual protections — the analyzer pack,
/// the architecture suite, the wire-shape tests — do not apply. They are constants here so a rename
/// is at least a single edit, and they are documented as frozen so it is a decision rather than a
/// refactor.
/// </para>
/// <para>
/// <b>Payload shapes live beside them in this namespace</b> and follow B13.1's append-don't-insert
/// rule: a new field goes at the end, an existing one never changes meaning, and nothing is removed
/// while a client in the wild might still read it.
/// </para>
/// </summary>
public static class RealtimeEvents
{
    /// <summary>
    /// The server's answer to a successful connection: who it thinks you are, what it can do.
    /// <para>
    /// Sent unprompted immediately after the socket is accepted. A client that never receives it is
    /// connected to something that is not this platform, which is a far better failure than a socket
    /// that looks healthy and delivers nothing.
    /// </para>
    /// </summary>
    public const string ConnectionAcknowledged = "ConnectionAcknowledged";

    /// <summary>A notification arrived. Payload: <see cref="RealtimeNotification"/>.</summary>
    public const string NotificationReceived = "NotificationReceived";

    /// <summary>The unread badge moved. Payload: <see cref="InboxCounters"/>.</summary>
    public const string InboxCountersChanged = "InboxCountersChanged";

    /// <summary>Somebody started or stopped typing. Payload: <see cref="TypingSignal"/>.</summary>
    public const string TypingChanged = "TypingChanged";

    /// <summary>Somebody's availability changed. Payload: <see cref="PresenceChanged"/>.</summary>
    public const string PresenceChanged = "PresenceChanged";

    /// <summary>A message was read. Payload: <see cref="ReadReceipt"/>.</summary>
    public const string ReadReceiptReceived = "ReadReceiptReceived";

    /// <summary>A message reached a device. Payload: <see cref="DeliveryReceipt"/>.</summary>
    public const string DeliveryReceiptReceived = "DeliveryReceiptReceived";

    /// <summary>
    /// A conversation message arrived. Payload is supplied by whichever module owns conversations.
    /// <para>
    /// <b>Declared here and never raised by this module.</b> The runtime carries messages; it does
    /// not own them. Naming the event now is what lets the vertical that eventually does own them
    /// ship without a client change — it calls <see cref="IRealtimeNotifier"/> with this name and
    /// its own payload.
    /// </para>
    /// </summary>
    public const string MessageReceived = "MessageReceived";

    /// <summary>
    /// A record the member is watching changed. Payload: <see cref="ResourceChanged"/>.
    /// <para>
    /// <b>One event for every domain rather than one method per domain, and the choice is
    /// deliberate.</b> The frontend registers asked for <c>PaymentStatusChanged</c>,
    /// <c>WalletBalanceChanged</c>, <c>occurrence.cancelled</c>, <c>membership.status.changed</c> and
    /// a dozen more, each on its own client method. This interface is a frozen wire contract that no
    /// compiler checks and every shipped mobile build matches by string — growing it once per domain
    /// event would mean a client release for each, and a platform where the newest verticals are the
    /// ones whose updates never arrive.
    /// </para>
    /// <para>
    /// <b>What a client does with it is invalidate, not render.</b> Every one of those requests was a
    /// cache-invalidation request wearing a domain name: the reconciliation loop that polls a payment
    /// wants to stop polling, and the screen showing a cancelled event wants to refetch. The
    /// authoritative representation is the REST resource, always — this says only that reading it
    /// again would now return something different, which is exactly what a live transport is entitled
    /// to claim.
    /// </para>
    /// </summary>
    public const string ResourceChanged = "ResourceChanged";
}
