using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// What the platform remembers about availability after the socket has gone.
/// <para>
/// <b>Separate from <see cref="IRealtimeConnectionRegistry"/> because the two answer questions with
/// different lifetimes.</b> The registry knows about sockets and forgets them the instant they
/// close. This knows that somebody was last seen at 14:02, which has to survive the socket, the
/// process and — in a deployment that cares — the instance. Folding them together works right up
/// until a member reconnects to a different node and their own last-seen time goes backwards.
/// </para>
/// <para>
/// <b>Nothing here decides who may look.</b> That is <see cref="IRealtimeGroupAuthorizer"/>'s job,
/// and keeping the two apart is what stops a privacy rule from being enforced in one of the three
/// places presence is read.
/// </para>
/// </summary>
public interface IPresenceStore
{
    /// <summary>
    /// The current answer for one member, live sockets included.
    /// <para>
    /// Never null: a member nobody has ever seen is <c>offline</c> with no last-seen, which is a
    /// complete answer rather than a missing one.
    /// </para>
    /// </summary>
    Task<PresenceSnapshot> GetAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The same question for several members at once.
    /// <para>
    /// A conversation list renders twenty avatars, and twenty round trips to answer one screen is
    /// the shape that makes presence expensive enough to switch off.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PresenceSnapshot>> GetManyAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a member's availability changed, and returns what it now is.
    /// <para>
    /// Called on connect, on disconnect and when idleness tips somebody into <c>Away</c>. Returns
    /// null when nothing actually changed, so a caller can skip a broadcast rather than telling
    /// every observer that an online member is still online.
    /// </para>
    /// </summary>
    Task<PresenceSnapshot?> RecordAsync(
        UserId userId,
        PresenceState state,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
