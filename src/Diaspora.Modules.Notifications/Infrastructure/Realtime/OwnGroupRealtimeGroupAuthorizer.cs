using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// A member may join their own group and observe their own availability. Nothing else.
/// <para>
/// <b>Retained, and no longer what ships.</b> Until B13.3 this was the registered default and the
/// only correct answer available: a conversation group is a set of participants, and nothing owned
/// conversations. B13.2 then replaced it wholesale, exactly as this file used to instruct — and that
/// instruction was the defect. A seam only one module can use is not an extension point: the second
/// vertical to register an <see cref="IRealtimeGroupAuthorizer"/> would have replaced the
/// conversation one, and every thread on the platform would have stopped relaying with nothing
/// logged and no test failing.
/// </para>
/// <para>
/// <b>What replaced it does the same thing, prefix by prefix.</b>
/// <c>CompositeRealtimeGroupAuthorizer</c> asks whichever module claimed a channel family, and
/// <c>UserRealtimeChannelAuthorizer</c> is this class's rule for <c>user:</c> expressed as a
/// contributor. The behaviour a deployment sees with no verticals installed is identical.
/// </para>
/// <para>
/// <b>This class stays because a deployment may deliberately want the narrow behaviour</b> — a
/// notification-only host with no conversations, no organizations and no appetite for a channel it
/// did not review — and registering it after the module restores exactly that. Nothing in the
/// platform registers it.
/// </para>
/// </summary>
public sealed class OwnGroupRealtimeGroupAuthorizer : IRealtimeGroupAuthorizer
{
    public Task<bool> CanJoinAsync(
        UserId userId, string group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (string.IsNullOrWhiteSpace(group))
        {
            return Task.FromResult(false);
        }

        // Parsed rather than compared as a string. A client supplies this, and "starts with user:"
        // is satisfied by "user:" followed by anything at all.
        var owner = RealtimeGroups.UserOf(group);

        return Task.FromResult(owner is not null && owner.Value == userId.Value);
    }

    public Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(subject);

        return Task.FromResult(observer.Value == subject.Value);
    }
}
