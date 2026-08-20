using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// Who is connected, and on how many sockets.
/// <para>
/// <b>A port, and the seam that decides whether this platform can run on more than one instance.</b>
/// The shipped implementation is an in-memory dictionary, which is correct and complete for a single
/// process and answers only for that process. A deployment that runs three instances replaces this
/// with one backed by a shared store and every caller above it — presence, the per-user connection
/// cap, the operator's "who is online" — becomes correct across the fleet without changing a line.
/// </para>
/// <para>
/// <b>Registration is not authorisation.</b> A connection reaches this interface only after the hub
/// has authenticated it; nothing here checks a token, and nothing here should. Its job is
/// bookkeeping.
/// </para>
/// <para>
/// Implementations must tolerate being asked to remove a connection twice. A socket can be reported
/// closed by both the transport and a client's own goodbye, and a registry that threw on the second
/// would take down a disconnect handler.
/// </para>
/// </summary>
public interface IRealtimeConnectionRegistry
{
    /// <summary>
    /// Records a newly accepted socket.
    /// <para>
    /// Returns the number of connections the member now holds, so the caller can enforce a cap
    /// without a second round trip.
    /// </para>
    /// </summary>
    Task<int> AddAsync(RealtimeConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Forgets a socket. Safe to call for one that was never added.</summary>
    Task RemoveAsync(string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes that a client did something. Drives the <c>Online</c> / <c>Away</c> distinction.
    /// </summary>
    Task TouchAsync(
        string connectionId, DateTimeOffset at, CancellationToken cancellationToken = default);

    /// <summary>Every live socket for one member. Empty is the ordinary answer.</summary>
    Task<IReadOnlyList<RealtimeConnection>> ForUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Whether the member holds at least one live socket.</summary>
    Task<bool> IsConnectedAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// How many sockets this instance is holding in total. An operational number, not a member one.
    /// </summary>
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
