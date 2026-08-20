using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// One live client socket, as the platform knows it.
/// <para>
/// <b>Deliberately not a SignalR type.</b> Everything above <see cref="IRealtimeConnectionRegistry"/>
/// reasons about connections — presence, fan-out, per-user limits, the operator's "who is online"
/// question — and none of it should have to be rewritten to move onto a different transport. The
/// connection identifier is an opaque string precisely so it can be a SignalR connection id today
/// and something else tomorrow.
/// </para>
/// <para>
/// <b>No token, no address, no user agent.</b> A connection record is operational data that lives in
/// memory on every instance and is read by an operator endpoint; nothing that identifies a device
/// beyond what the member already told us belongs in it.
/// </para>
/// </summary>
/// <param name="ConnectionId">The transport's own opaque handle.</param>
/// <param name="UserId">Who is on the other end. Never null — anonymous connections are refused.</param>
/// <param name="Platform">What kind of client, when it said. <c>Unknown</c> when it did not.</param>
/// <param name="ConnectedAt">When the socket was accepted.</param>
/// <param name="LastActivityAt">
/// When the client last did anything — a hub call, a heartbeat. What separates <c>Online</c> from
/// <c>Away</c>, and the reason presence is not simply "has a socket".
/// </param>
public sealed record RealtimeConnection(
    string ConnectionId,
    UserId UserId,
    DevicePlatformKind Platform,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastActivityAt);
