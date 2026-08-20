using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// Everything the hub does, with no hub in it.
/// <para>
/// <b>Separate from <see cref="RealtimeHub"/> so the rules can be tested without a socket.</b> A hub
/// method is hard to exercise — it wants a connection context, a caller proxy and a lifetime
/// manager — and rules that are hard to exercise are rules that get one happy-path test. Connection
/// caps, presence transitions, group authorisation and the three relays all live here and are
/// ordinary unit tests.
/// </para>
/// <para>
/// <b>It refuses rather than repairs.</b> Every method answers "may this happen" and does the work
/// or does not; none of them throws for an ordinary refusal. A hub that threw would return an error
/// frame to the client for the routine case of asking about a conversation you are not in, and a
/// client cannot tell that apart from a fault.
/// </para>
/// </summary>
public sealed class RealtimeConnectionCoordinator
{
    private readonly IRealtimeConnectionRegistry _connections;
    private readonly IPresenceStore _presence;
    private readonly IRealtimeNotifier _notifier;
    private readonly IRealtimeGroupAuthorizer _authorizer;
    private readonly RealtimeChannelRegistry _channels;
    private readonly IOptionsMonitor<RealtimeOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<RealtimeConnectionCoordinator> _logger;

    public RealtimeConnectionCoordinator(
        IRealtimeConnectionRegistry connections,
        IPresenceStore presence,
        IRealtimeNotifier notifier,
        IRealtimeGroupAuthorizer authorizer,
        RealtimeChannelRegistry channels,
        IOptionsMonitor<RealtimeOptions> options,
        IClock clock,
        ILogger<RealtimeConnectionCoordinator> logger)
    {
        _connections = connections;
        _presence = presence;
        _notifier = notifier;
        _authorizer = authorizer;
        _channels = channels;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public RealtimeOptions Options => _options.CurrentValue;

    /// <summary>
    /// Whether anything in this deployment can say who is in a conversation.
    /// <para>
    /// <b>Read from what is installed rather than from configuration, because it is not a deployment
    /// choice.</b> An option would be worse than useless: an operator could set it true with nothing
    /// behind it, and every client would subscribe to groups that relay nothing — the exact failure
    /// the flag exists to prevent a client from walking into.
    /// </para>
    /// <para>
    /// <b>B13.3 changed how the question is answered, and the previous answer was the reason.</b> It
    /// used to be an identity test — "the registered authorizer is not the shipped own-group one" —
    /// which was true exactly while one module could own the conversation family by replacing the
    /// default. With the prefix registry any number of modules contribute, so a type check would read
    /// true on a deployment that had a channel registry and no conversations in it, and every client
    /// would be told to subscribe to threads that relay nothing. Asking the registry whether anybody
    /// claimed <c>conversation:</c> answers the question that was always being asked.
    /// </para>
    /// </summary>
    public bool ConversationRelayEnabled => _channels.IsOwned(RealtimeGroups.ConversationPrefix);

    /// <summary>
    /// Registers a newly accepted socket and produces the handshake the client is sent.
    /// <para>
    /// Returns null when the member is already holding <c>MaxConnectionsPerUser</c> sockets, which
    /// the hub turns into a refused connection. <b>The newest is refused rather than the oldest
    /// closed</b>, because the transport offers no way to abort another connection from here — and
    /// the alternative, asking a client to disconnect itself, is a request a client stuck in a
    /// reconnect loop is by definition not honouring. The refused client retries; the sockets it
    /// abandoned expire on the client timeout and free their slots.
    /// </para>
    /// </summary>
    public async Task<RealtimeHandshake?> OnConnectedAsync(
        string connectionId,
        UserId userId,
        DevicePlatformKind platform,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionId);
        ArgumentNullException.ThrowIfNull(userId);

        var options = Options;
        var now = _clock.UtcNow;

        var held = await _connections.ForUserAsync(userId, cancellationToken);

        if (held.Count >= options.MaxConnectionsPerUser)
        {
            _logger.LogInformation(
                "Refused a real-time connection: the member already holds {Count} of {Max} permitted.",
                held.Count,
                options.MaxConnectionsPerUser);

            NotificationTelemetry.RealtimeRefused.Add(1);

            return null;
        }

        await _connections.AddAsync(
            new RealtimeConnection(connectionId, userId, platform, now, now), cancellationToken);

        NotificationTelemetry.RealtimeConnected.Add(
            1, new KeyValuePair<string, object?>("platform", platform.ToString()));

        // The first socket is the transition; the second and third are the same member on another
        // device and change nothing anybody needs telling about.
        if (held.Count == 0)
        {
            await BroadcastPresenceAsync(userId, PresenceState.Online, now, cancellationToken);
        }

        return new RealtimeHandshake(
            connectionId,
            userId.Value,
            now,
            options.Presence.Enabled,
            options.Typing.Enabled,
            options.Receipts.EnableReadReceipts,
            options.Receipts.EnableDeliveryReceipts,
            ConversationRelayEnabled);
    }

    /// <summary>
    /// Forgets a socket, and reports the member offline when it was their last.
    /// </summary>
    public async Task OnDisconnectedAsync(
        string connectionId, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        await _connections.RemoveAsync(connectionId, cancellationToken);

        NotificationTelemetry.RealtimeDisconnected.Add(1);

        var remaining = await _connections.ForUserAsync(userId, cancellationToken);

        if (remaining.Count == 0)
        {
            await BroadcastPresenceAsync(
                userId, PresenceState.Offline, _clock.UtcNow, cancellationToken);
        }
    }

    /// <summary>Notes that a client did something, which is what keeps it out of <c>away</c>.</summary>
    public Task TouchAsync(string connectionId, CancellationToken cancellationToken = default) =>
        _connections.TouchAsync(connectionId, _clock.UtcNow, cancellationToken);

    /// <summary>
    /// Whether the caller may join a group. The hub joins only after this says yes.
    /// </summary>
    public Task<bool> CanJoinAsync(
        UserId userId, string group, CancellationToken cancellationToken = default) =>
        _authorizer.CanJoinAsync(userId, group, cancellationToken);

    /// <summary>
    /// Relays a typing signal to the rest of a conversation.
    /// <para>
    /// Returns false when typing is switched off or the caller is not in the group — the hub answers
    /// the client either way, because a client that cannot tell "refused" from "delivered" leaves
    /// "typing…" on screen forever.
    /// </para>
    /// </summary>
    public async Task<bool> RelayTypingAsync(
        UserId userId,
        string connectionId,
        Guid conversationId,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (!Options.Typing.Enabled || conversationId == Guid.Empty)
        {
            return false;
        }

        var group = RealtimeGroups.ForConversation(conversationId);

        if (!await CanJoinAsync(userId, group, cancellationToken))
        {
            return false;
        }

        var signal = new TypingSignal(Normalise(eventType), conversationId, userId.Value, _clock.UtcNow);

        // Excluding the sender's own socket rather than the sender: a member typing on their phone
        // while their tablet is open should see the indicator on the tablet, exactly as they would
        // see their own message there.
        await _notifier.SendToGroupExceptAsync(
            group, connectionId, RealtimeEvents.TypingChanged, signal, cancellationToken);

        return true;
    }

    /// <summary>Relays a read receipt to the rest of a conversation.</summary>
    public async Task<bool> RelayReadReceiptAsync(
        UserId userId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!Options.Receipts.EnableReadReceipts
            || conversationId == Guid.Empty
            || messageId == Guid.Empty)
        {
            return false;
        }

        var group = RealtimeGroups.ForConversation(conversationId);

        if (!await CanJoinAsync(userId, group, cancellationToken))
        {
            return false;
        }

        // The reader is taken from the authenticated connection, never from the frame. A client that
        // could name who read something could mark somebody else's message read on their behalf.
        var receipt = new ReadReceipt(conversationId, messageId, userId.Value, _clock.UtcNow);

        await _notifier.SendToGroupAsync(
            group, RealtimeEvents.ReadReceiptReceived, receipt, cancellationToken);

        return true;
    }

    /// <summary>Relays a delivery acknowledgement to the rest of a conversation.</summary>
    public async Task<bool> RelayDeliveryReceiptAsync(
        UserId userId,
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        if (!Options.Receipts.EnableDeliveryReceipts
            || conversationId == Guid.Empty
            || messageId == Guid.Empty)
        {
            return false;
        }

        var group = RealtimeGroups.ForConversation(conversationId);

        if (!await CanJoinAsync(userId, group, cancellationToken))
        {
            return false;
        }

        var receipt = new DeliveryReceipt(conversationId, messageId, userId.Value, _clock.UtcNow);

        await _notifier.SendToGroupAsync(
            group, RealtimeEvents.DeliveryReceiptReceived, receipt, cancellationToken);

        return true;
    }

    /// <summary>
    /// The availability of the members the caller is permitted to see.
    /// <para>
    /// Members the caller may not observe are <em>omitted</em> rather than reported offline. Saying
    /// "offline" would be a lie a client renders, and reporting a refusal per identifier would let a
    /// caller enumerate who exists.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PresenceSnapshot>> ObservePresenceAsync(
        UserId observer,
        IReadOnlyCollection<UserId> subjects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(subjects);

        if (!Options.Presence.Enabled || subjects.Count == 0)
        {
            return [];
        }

        var permitted = new List<UserId>(subjects.Count);

        foreach (var subject in subjects)
        {
            if (await _authorizer.CanObservePresenceAsync(observer, subject, cancellationToken))
            {
                permitted.Add(subject);
            }
        }

        return permitted.Count == 0
            ? []
            : await _presence.GetManyAsync(permitted, cancellationToken);
    }

    /// <summary>
    /// Records a presence transition and tells the member's own devices.
    /// <para>
    /// <b>Only their own devices, and that is a real limit rather than an oversight.</b> Telling
    /// anybody else requires knowing who is watching, and an observer index is a subscription
    /// relationship no module owns yet — the same gap that leaves conversation groups unjoinable.
    /// The store is authoritative in the meantime: a client that needs somebody else's availability
    /// asks for it.
    /// </para>
    /// </summary>
    private async Task BroadcastPresenceAsync(
        UserId userId, PresenceState state, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var snapshot = await _presence.RecordAsync(userId, state, at, cancellationToken);

        if (snapshot is null)
        {
            return;
        }

        await _notifier.SendToUserAsync(
            userId,
            RealtimeEvents.PresenceChanged,
            new PresenceChanged(userId.Value, snapshot.Status, snapshot.LastSeenAt, at),
            cancellationToken);
    }

    /// <summary>
    /// Maps whatever the client sent onto the three signals the contract defines.
    /// <para>
    /// Anything unrecognised becomes <c>TypingEnded</c>, which is the safe direction: a stray value
    /// clears an indicator rather than pinning one on screen.
    /// </para>
    /// </summary>
    private static string Normalise(string? eventType) => eventType switch
    {
        TypingSignal.Started => TypingSignal.Started,
        TypingSignal.Updated => TypingSignal.Updated,
        _ => TypingSignal.Ended
    };
}
