namespace Diaspora.Modules.Notifications.Options;

/// <summary>
/// The real-time runtime's knobs.
/// <para>
/// <b>Bound from <c>Notifications:Realtime</c>, and every default here is a working single-instance
/// deployment.</b> A fresh clone gets an authenticated hub, presence, typing relay and receipts with
/// no configuration at all — which is the same posture B13 took for the delivery sweeps, and for the
/// same reason: a runtime that needs to be switched on is a runtime that is off in the environment
/// nobody remembered to configure.
/// </para>
/// <para>
/// <b>Nothing here names a transport.</b> There is no <c>UseWebSockets</c>, no hub protocol, no
/// vendor. Those are properties of whichever implementation serves
/// <c>IRealtimeNotifier</c>; this is the platform's own policy about presence, idleness, limits and
/// what a client is allowed to relay.
/// </para>
/// </summary>
public sealed class RealtimeOptions
{
    public const string SectionName = "Notifications:Realtime";

    /// <summary>
    /// Whether the hubs are mapped at all.
    /// <para>
    /// Off means the endpoints do not exist, the in-app channel skips its live fan-out and every
    /// notification still lands in the inbox exactly as it does today. The switch is here so a
    /// deployment that cannot run WebSockets — behind a proxy that does not forward upgrades, in a
    /// worker process — is a configuration rather than a fork.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many instances this deployment runs.
    /// <para>
    /// <b>Stated rather than detected, and it is the one value an operator must not get wrong.</b>
    /// Nothing at runtime can tell how many copies of itself exist, so a platform that guessed would
    /// guess "one" and be silently wrong on every scaled deployment — half the members attached to
    /// the other node, no notification reaching them, nothing logged. Set above one without a
    /// backplane and the host refuses to start.
    /// </para>
    /// </summary>
    public int InstanceCount { get; set; } = 1;

    /// <summary>
    /// Sockets one member may hold at once.
    /// <para>
    /// Ten. A person legitimately has a phone, a tablet and two browser tabs; a client stuck in a
    /// reconnect loop opens forty, and the cap is what stops one broken build from consuming an
    /// instance's file descriptors. The oldest connection is closed rather than the newest refused —
    /// the member is looking at the newest.
    /// </para>
    /// </summary>
    public int MaxConnectionsPerUser { get; set; } = 10;

    /// <summary>
    /// How often the server pings an idle socket, in seconds.
    /// <para>
    /// Fifteen, matching the SignalR default, because intermediate proxies commonly drop a
    /// connection idle for thirty and the keep-alive is what stops that looking like a member going
    /// offline.
    /// </para>
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 15;

    /// <summary>
    /// How long the server waits to hear anything before treating a socket as gone, in seconds.
    /// <para>
    /// Thirty — twice the keep-alive, which is the documented relationship. Shortening it below that
    /// disconnects healthy clients on a slow network.
    /// </para>
    /// </summary>
    public int ClientTimeoutSeconds { get; set; } = 30;

    /// <summary>How long a client has to complete the handshake, in seconds.</summary>
    public int HandshakeTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// The largest message a client may send, in bytes.
    /// <para>
    /// 32 KB, which is generous for the four things a client is allowed to send — a group join, a
    /// typing signal and two receipts — and small enough that a hostile client cannot buy memory
    /// with a single frame. Message <em>bodies</em> do not travel up this socket; they travel over
    /// HTTP to whichever module owns them.
    /// </para>
    /// </summary>
    public int MaxReceiveMessageBytes { get; set; } = 32 * 1024;

    /// <summary>
    /// Whether hub exceptions are returned to the client.
    /// <para>
    /// Off. A hub exception message can carry an identifier, a query fragment or a member's own
    /// data, and it reaches a device the platform does not control. Development turns it on locally
    /// through configuration; nothing turns it on by default.
    /// </para>
    /// </summary>
    public bool EnableDetailedErrors { get; set; }

    public PresenceOptions Presence { get; set; } = new();

    public TypingOptions Typing { get; set; } = new();

    public ReceiptOptions Receipts { get; set; } = new();

    public RealtimeBackplaneOptions Backplane { get; set; } = new();
}

/// <summary>Presence policy. Bound from <c>Notifications:Realtime:Presence</c>.</summary>
public sealed class PresenceOptions
{
    /// <summary>
    /// Whether availability is tracked and broadcast.
    /// <para>
    /// A deployment may switch this off wholesale — presence is the most privacy-sensitive thing the
    /// runtime produces, and some jurisdictions and some products simply do not want it. With it
    /// off, the connection registry still tracks sockets (fan-out needs that) and nobody is told
    /// about anybody.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Idle seconds before a connected member is reported <c>away</c> rather than <c>online</c>.
    /// <para>
    /// Five minutes. A socket stays open in a pocket for far longer than a person stays engaged, and
    /// a green dot that means "their phone is on" is a green dot members learn to ignore.
    /// </para>
    /// </summary>
    public int AwayAfterSeconds { get; set; } = 300;

    /// <summary>
    /// Seconds to wait after the last socket closes before reporting <c>offline</c>.
    /// <para>
    /// Ten. A mobile client reconnects constantly — a lift, a tunnel, a network handover — and
    /// broadcasting offline/online on each one produces a flickering dot and a broadcast storm for
    /// no information gained.
    /// </para>
    /// </summary>
    public int OfflineGraceSeconds { get; set; } = 10;

    /// <summary>
    /// How long a last-seen time is remembered after a member disconnects, in minutes.
    /// <para>
    /// A day. Beyond that the honest answer is "offline" with no time — "last seen 3 weeks ago" is
    /// disclosure rather than information, and the entry is memory this instance is paying for.
    /// </para>
    /// </summary>
    public int LastSeenRetentionMinutes { get; set; } = 1440;
}

/// <summary>Typing relay policy. Bound from <c>Notifications:Realtime:Typing</c>.</summary>
public sealed class TypingOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Seconds after which a client should forget a typing signal it never saw ended.
    /// <para>
    /// Advertised to clients rather than enforced here: the server relays and forgets, and the
    /// timeout is the client's business. It is configuration so that "typing…" cannot be left on
    /// screen forever by one dropped frame.
    /// </para>
    /// </summary>
    public int TimeoutSeconds { get; set; } = 8;

    /// <summary>
    /// Typing signals one connection may send per minute.
    /// <para>
    /// Sixty. A well-behaved client sends one every few seconds while the member types; a broken or
    /// hostile one sends one per keystroke, and this is what makes that cheap to refuse.
    /// </para>
    /// </summary>
    public int MaxSignalsPerMinute { get; set; } = 60;
}

/// <summary>Receipt relay policy. Bound from <c>Notifications:Realtime:Receipts</c>.</summary>
public sealed class ReceiptOptions
{
    /// <summary>Whether read receipts are relayed.</summary>
    public bool EnableReadReceipts { get; set; } = true;

    /// <summary>Whether delivery acknowledgements are relayed.</summary>
    public bool EnableDeliveryReceipts { get; set; } = true;
}

/// <summary>
/// What carries fan-out between instances. Bound from <c>Notifications:Realtime:Backplane</c>.
/// <para>
/// <b>No connection string is read by this module.</b> A backplane is wired in beneath the hub by
/// whichever adapter a deployment installs, and that adapter reads its own configuration. What lives
/// here is the deployment's <em>statement</em> about what it installed, which is what the startup
/// validator checks against <see cref="RealtimeOptions.InstanceCount"/>.
/// </para>
/// </summary>
public sealed class RealtimeBackplaneOptions
{
    /// <summary>
    /// The backplane's name. <c>None</c> means single-instance and is the default.
    /// <para>
    /// Any other value is treated as distributed. The name is free text rather than an enum on
    /// purpose: the set of things that can carry SignalR fan-out is not this module's to enumerate,
    /// and an enum would make adding one a change to the Communication Platform.
    /// </para>
    /// </summary>
    public string Provider { get; set; } = "None";
}
