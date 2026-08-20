using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// The one way anything in this platform pushes to a live client.
/// <para>
/// <b>This interface is the whole of "no real-time provider lock-in", and it is deliberately
/// tiny.</b> Send to a member, send to a group. Everything above it — the in-app channel's live
/// fan-out, typing relay, presence broadcast, read receipts — is written once against these two
/// methods and knows nothing about SignalR, WebSockets, hubs, connection identifiers or transports.
/// The shipped implementation is a SignalR hub context; a deployment that moves to a hosted
/// real-time service replaces this one class.
/// </para>
/// <para>
/// <b>Delivery is best-effort by definition and never throws for an absent recipient.</b> A member
/// with no live socket is the normal case, not a failure — the message is already in their inbox and
/// push exists for exactly this. An implementation that threw would make every caller wrap it, and
/// the first caller that forgot would fail a notification because somebody's phone was in a tunnel.
/// </para>
/// <para>
/// <b>Group names come from <see cref="RealtimeGroups"/> and are never composed at a call site.</b>
/// A group name is an authorisation boundary; two spellings of one boundary is a member receiving
/// somebody else's typing indicator.
/// </para>
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Whether a live transport is configured at all.
    /// <para>
    /// False makes every send a no-op. Callers use it to skip work rather than to decide policy —
    /// the in-app channel still succeeds with real-time switched off, because the inbox write is
    /// what in-app delivery means.
    /// </para>
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Sends to every socket the member currently holds, on every instance.
    /// </summary>
    /// <param name="userId">The recipient.</param>
    /// <param name="eventName">One of <see cref="RealtimeEvents"/>. Never a literal at a call site.</param>
    /// <param name="payload">A contract record from this namespace. Serialised by the transport.</param>
    Task SendToUserAsync(
        UserId userId, string eventName, object payload, CancellationToken cancellationToken = default);

    /// <summary>Sends to everybody currently joined to a group.</summary>
    Task SendToGroupAsync(
        string group, string eventName, object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends to a group, excluding one connection — the sender's own.
    /// <para>
    /// Typing is the reason this overload exists: echoing your own keystrokes back at you makes a
    /// client render "you are typing…" to yourself, and every client would otherwise have to filter.
    /// </para>
    /// </summary>
    Task SendToGroupExceptAsync(
        string group,
        string exceptConnectionId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
