namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// What a client needs to know before it opens a socket.
/// <para>
/// <b>Fetched over HTTP rather than discovered by connecting, because a client has to decide whether
/// to connect at all.</b> A deployment with real-time switched off, or with typing relay disabled,
/// should not have an app that opens a socket, waits for a handshake and then quietly does nothing
/// with it — that is a reconnect loop nobody can diagnose from the client side. One request answers
/// it, and the same flags come back on the handshake so a client that connected before the answer
/// arrived is not left guessing.
/// </para>
/// <para>
/// <b><see cref="HubPath"/> is served rather than hard-coded in the app</b> so the one thing that
/// would strand every installed build — a moved endpoint — is a server-side change.
/// </para>
/// </summary>
public sealed record RealtimeConfigurationResponse
{
    /// <summary>Whether the runtime is available at all. False means do not open a socket.</summary>
    public required bool Enabled { get; init; }

    /// <summary>Where the hub is mapped, relative to the API host.</summary>
    public required string HubPath { get; init; }

    /// <summary>Whether availability is tracked and reported.</summary>
    public required bool PresenceEnabled { get; init; }

    /// <summary>Whether typing signals are relayed.</summary>
    public required bool TypingEnabled { get; init; }

    /// <summary>
    /// How long a client should keep showing "typing…" without a further signal, in seconds.
    /// <para>
    /// Served rather than assumed, so the indicator's lifetime and the relay's expectations cannot
    /// drift apart across app versions.
    /// </para>
    /// </summary>
    public required int TypingTimeoutSeconds { get; init; }

    /// <summary>Whether read receipts are relayed.</summary>
    public required bool ReadReceiptsEnabled { get; init; }

    /// <summary>Whether delivery acknowledgements are relayed.</summary>
    public required bool DeliveryReceiptsEnabled { get; init; }

    /// <summary>
    /// Whether a client may join conversation groups.
    /// <para>
    /// False on every shipped deployment: no module owns conversations, so nothing can say who is in
    /// one, and the runtime refuses rather than relaying a member's typing to whoever guessed an
    /// identifier. A client should render its conversation screens against HTTP and treat live
    /// updates as an enhancement it does not yet have.
    /// </para>
    /// </summary>
    public required bool ConversationRelayEnabled { get; init; }

    /// <summary>Seconds between client heartbeats, so idleness is measured consistently.</summary>
    public required int HeartbeatSeconds { get; init; }

    /// <summary>Sockets one member may hold at once. A client opening more is refused.</summary>
    public required int MaxConnectionsPerUser { get; init; }
}
