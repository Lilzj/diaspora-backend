using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// Decides whether a member may join a real-time group or observe another member's presence.
/// <para>
/// <b>The seam that keeps the runtime honest about what it does not own.</b> A hub can authenticate
/// a connection — that is Identity's answer and the hub gets it for free. It cannot answer "is this
/// person a participant in conversation 4f21…", because nothing in this platform owns conversations
/// yet. A runtime that let a client name any group and join it would relay typing indicators and
/// read receipts to anybody who guessed an identifier, and it would do it silently.
/// </para>
/// <para>
/// <b>The shipped implementation permits a member's own user group and refuses everything else.</b>
/// That is not a placeholder standing in for a missing check — it is the correct answer while no
/// module can supply one. The vertical that eventually owns conversations registers its own
/// implementation after this module, and the relay starts working with no change to the hubs, the
/// notifier or the client contract.
/// </para>
/// <para>
/// Implementations are consulted on every join and on every presence read, so they must be cheap or
/// cached. They are asked, never told: returning false is an ordinary answer and must not throw.
/// </para>
/// </summary>
public interface IRealtimeGroupAuthorizer
{
    /// <summary>
    /// Whether <paramref name="userId"/> may join <paramref name="group"/>.
    /// </summary>
    /// <param name="userId">The authenticated caller.</param>
    /// <param name="group">A name produced by <see cref="RealtimeGroups"/>.</param>
    Task<bool> CanJoinAsync(
        UserId userId, string group, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether <paramref name="observer"/> may be told about <paramref name="subject"/>'s
    /// availability.
    /// <para>
    /// Separate from <see cref="CanJoinAsync"/> because presence leaks a different thing: group
    /// membership discloses a conversation, presence discloses a person's habits. A deployment may
    /// reasonably allow one and refuse the other.
    /// </para>
    /// </summary>
    Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default);
}
