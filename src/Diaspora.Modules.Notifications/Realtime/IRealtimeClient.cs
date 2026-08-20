using Diaspora.Modules.Notifications.Extensibility.Realtime;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// What the server may call on a connected client. The typed half of the wire contract.
/// <para>
/// <b>Declared so that calls made from inside the hub are compiler-checked.</b> A hub that sends by
/// string can misspell an event name, pass the wrong payload shape, or add an argument, and every
/// one of those compiles and fails silently on a device nobody is holding. With
/// <c>Hub&lt;IRealtimeClient&gt;</c> the handshake, the typing relay and both receipts are ordinary
/// method calls the compiler checks.
/// </para>
/// <para>
/// <b>The method names must equal the constants in <see cref="RealtimeEvents"/>.</b> SignalR
/// dispatches by method name, so this interface and those constants are two spellings of one
/// contract — a unit test asserts they have not drifted, because nothing else would notice.
/// </para>
/// <para>
/// <b>Fan-out from outside the hub does not use this interface</b>, and deliberately: the seam has
/// to carry payloads this module does not define, so <see cref="IRealtimeNotifier"/> dispatches by
/// name. Both paths end at the same client method.
/// </para>
/// </summary>
public interface IRealtimeClient
{
    /// <summary>The server's answer to a successful connection.</summary>
    Task ConnectionAcknowledged(RealtimeHandshake handshake);

    /// <summary>A notification arrived while the client was connected.</summary>
    Task NotificationReceived(RealtimeNotification notification);

    /// <summary>The unread badge moved.</summary>
    Task InboxCountersChanged(InboxCounters counters);

    /// <summary>Somebody started or stopped typing.</summary>
    Task TypingChanged(TypingSignal signal);

    /// <summary>Somebody's availability changed.</summary>
    Task PresenceChanged(PresenceChanged presence);

    /// <summary>A message was read.</summary>
    Task ReadReceiptReceived(ReadReceipt receipt);

    /// <summary>A message reached a device.</summary>
    Task DeliveryReceiptReceived(DeliveryReceipt receipt);

    /// <summary>
    /// A record the member is watching changed and should be read again.
    /// <para>
    /// The one method that is not about notifications or conversations, and the only one a vertical
    /// other than those two ever needs. See <see cref="RealtimeEvents.ResourceChanged"/>.
    /// </para>
    /// </summary>
    Task ResourceChanged(ResourceChanged change);
}
