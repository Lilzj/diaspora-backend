using System.Security.Claims;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// The platform's single real-time endpoint.
/// <para>
/// <b>One hub, not three, and that is the deliberate divergence from the obvious design.</b> A
/// notification hub, a presence hub and a conversation hub would be three WebSocket connections from
/// one phone: three handshakes on every app resume, three reconnect loops on a network handover,
/// three sets of presence bookkeeping that can disagree about whether the same member is online, and
/// three times the battery. Nothing is gained — the concerns are separated by
/// <see cref="RealtimeConnectionCoordinator"/> and by the event names, which is where separation is
/// free.
/// </para>
/// <para>
/// <b>Authenticated, always.</b> <see cref="AuthorizeAttribute"/> with the module's recipient policy,
/// so an anonymous socket is refused at the handshake rather than after it has been accepted and
/// counted. The subject is read from the connection's own claims and never from a frame the client
/// sent: every method below derives the caller from <see cref="Hub.Context"/>, so a client cannot
/// act as somebody else by putting their identifier in an argument.
/// </para>
/// <para>
/// <b>The hub does no work.</b> Every method authenticates, delegates and returns. That keeps the
/// rules testable without a socket and means the code paths that decide anything are exercised by
/// unit tests rather than by an integration test that has to negotiate a transport first.
/// </para>
/// </summary>
// ASP.NET Core's [Authorize], not the CQRS one. A hub method does not travel through
// MediatR, so the pipeline would never read a Diaspora attribute here -- the framework
// evaluates this during the handshake. BSOLID-001's migration script rewrote it and only
// the deleted type caught the mistake; with the old attribute kept as an alias this would
// have compiled and left the socket unauthenticated.
[Authorize(Policy = NotificationPolicies.Recipient)]
public sealed class RealtimeHub : Hub<IRealtimeClient>
{
    /// <summary>
    /// The query-string key a client passes its platform on.
    /// <para>
    /// A query parameter rather than a hub call, because it must be known before the handshake
    /// completes — the connection record is written in <see cref="OnConnectedAsync"/>, and a
    /// platform learned a frame later would leave every connection's first moments unattributed.
    /// </para>
    /// </summary>
    public const string PlatformQueryKey = "platform";

    private readonly RealtimeConnectionCoordinator _coordinator;

    public RealtimeHub(RealtimeConnectionCoordinator coordinator) => _coordinator = coordinator;

    public override async Task OnConnectedAsync()
    {
        var userId = CallerId();

        if (userId is null)
        {
            // Authenticated by the policy but carrying no usable subject. Refusing beats accepting a
            // socket that can never be addressed — see RealtimeUserIdProvider for how that happens.
            Context.Abort();
            return;
        }

        var handshake = await _coordinator.OnConnectedAsync(
            Context.ConnectionId, userId, Platform(), Context.ConnectionAborted);

        if (handshake is null)
        {
            Context.Abort();
            return;
        }

        // Joined by the server, not requested by the client. A member's own group is the one
        // subscription that needs no authorisation and must never be forgotten — every notification
        // this platform pushes goes to it.
        await Groups.AddToGroupAsync(
            Context.ConnectionId, RealtimeGroups.ForUser(userId), Context.ConnectionAborted);

        await Clients.Caller.ConnectionAcknowledged(handshake);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CallerId();

        if (userId is not null)
        {
            // Not passing the cancellation token: Context.ConnectionAborted is already cancelled by
            // the time this runs, and honouring it would abandon the bookkeeping that makes the
            // member look permanently online.
            await _coordinator.OnDisconnectedAsync(Context.ConnectionId, userId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Asks to receive a group's traffic.
    /// <para>
    /// Returns whether the join happened. <b>False is an ordinary answer, not a fault</b> — on every
    /// shipped deployment it is the answer for every conversation group, because nothing can yet say
    /// who is in one. Throwing would give a client an error frame it cannot distinguish from a
    /// server fault.
    /// </para>
    /// </summary>
    public async Task<bool> JoinGroup(string group)
    {
        var userId = CallerId();

        if (userId is null || string.IsNullOrWhiteSpace(group))
        {
            return false;
        }

        await _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

        if (!await _coordinator.CanJoinAsync(userId, group, Context.ConnectionAborted))
        {
            return false;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        return true;
    }

    /// <summary>
    /// Stops receiving a group's traffic.
    /// <para>
    /// Unauthorised, on purpose: leaving a group you were never in changes nothing, and requiring
    /// permission to stop listening would strand a client whose access was revoked mid-connection.
    /// </para>
    /// </summary>
    public async Task LeaveGroup(string group)
    {
        if (!string.IsNullOrWhiteSpace(group))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        }
    }

    /// <summary>Reports that the member started, continued or stopped typing.</summary>
    public async Task<bool> Typing(Guid conversationId, string eventType)
    {
        var userId = CallerId();

        if (userId is null)
        {
            return false;
        }

        await _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

        return await _coordinator.RelayTypingAsync(
            userId, Context.ConnectionId, conversationId, eventType, Context.ConnectionAborted);
    }

    /// <summary>Reports that the member opened a message.</summary>
    public async Task<bool> MarkRead(Guid conversationId, Guid messageId)
    {
        var userId = CallerId();

        if (userId is null)
        {
            return false;
        }

        await _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

        return await _coordinator.RelayReadReceiptAsync(
            userId, conversationId, messageId, Context.ConnectionAborted);
    }

    /// <summary>
    /// Reports that a message reached this device.
    /// <para>
    /// Sent by the client rather than inferred by the server, because only the device knows it
    /// rendered something. The server knows it wrote to a socket, which is not the same claim.
    /// </para>
    /// </summary>
    public async Task<bool> AcknowledgeDelivery(Guid conversationId, Guid messageId)
    {
        var userId = CallerId();

        if (userId is null)
        {
            return false;
        }

        await _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

        return await _coordinator.RelayDeliveryReceiptAsync(
            userId, conversationId, messageId, Context.ConnectionAborted);
    }

    /// <summary>
    /// The availability of members the caller is permitted to observe.
    /// <para>
    /// A pull rather than a subscription. Broadcasting availability to observers needs an index of
    /// who is watching whom, which is a relationship no module owns — so a client asks when it
    /// renders a list, and is told about its own changes as they happen.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<PresenceSnapshot>> GetPresence(IReadOnlyList<Guid>? userIds)
    {
        var userId = CallerId();

        if (userId is null || userIds is null || userIds.Count == 0)
        {
            return [];
        }

        await _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

        var subjects = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(UserId.From)
            .ToList();

        return await _coordinator.ObservePresenceAsync(userId, subjects, Context.ConnectionAborted);
    }

    /// <summary>
    /// A no-op the client calls to say it is still there.
    /// <para>
    /// Distinct from the transport's own keep-alive, which proves the socket is open and says
    /// nothing about whether anybody is looking at it. This is what separates <c>online</c> from
    /// <c>away</c>, and it is why the distinction means something.
    /// </para>
    /// </summary>
    public Task Heartbeat() => _coordinator.TouchAsync(Context.ConnectionId, Context.ConnectionAborted);

    /// <summary>
    /// The authenticated subject, or null when the connection carries none.
    /// <para>
    /// Read from the claims every time rather than cached on the hub: a hub instance is created per
    /// invocation, so there is nothing to cache, and reading once per call is what guarantees a
    /// revoked token cannot leave a stale subject behind.
    /// </para>
    /// </summary>
    private UserId? CallerId()
    {
        var raw = Context.User?.FindFirst("sub")?.Value
            ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(raw, out var parsed) && parsed != Guid.Empty
            ? UserId.From(parsed)
            : null;
    }

    /// <summary>
    /// What kind of client connected, from the negotiate request's query string.
    /// <para>
    /// Unrecognised and absent both become <c>Unknown</c>, which is a complete answer — the platform
    /// value is used for telemetry and for an operator reading a connection list, never for a
    /// decision.
    /// </para>
    /// </summary>
    private DevicePlatformKind Platform()
    {
        var raw = Context.GetHttpContext()?.Request.Query[PlatformQueryKey].ToString();

        return Enum.TryParse<DevicePlatformKind>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : DevicePlatformKind.Unknown;
    }
}
