using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// The shipped answer to "may this member join this group", assembled from whoever owns the subject.
/// <para>
/// <b>The port did not change; what stands behind it did.</b> <see cref="IRealtimeGroupAuthorizer"/>
/// remains the one question the hub asks, so the coordinator, the hub and every test that stubs the
/// port are untouched. This implementation stops answering the question itself and asks the module
/// that owns the channel family instead — which is the difference between a seam one vertical can use
/// and a seam every vertical can.
/// </para>
/// <para>
/// <b>Why the old default had to go.</b> <c>OwnGroupRealtimeGroupAuthorizer</c> answered for every
/// prefix, permitting <c>user:</c> and refusing the rest. A module that wanted a channel of its own
/// could therefore only participate by replacing it wholesale — which B13.2 did, correctly and as
/// documented. The second module to do so would have replaced B13.2's, and every conversation on the
/// platform would have stopped relaying with nothing logged and no test failing. That class is
/// retained for a deployment that deliberately wants the narrow behaviour, and is no longer what
/// ships.
/// </para>
/// </summary>
public sealed class CompositeRealtimeGroupAuthorizer : IRealtimeGroupAuthorizer
{
    private readonly RealtimeChannelRegistry _channels;

    public CompositeRealtimeGroupAuthorizer(RealtimeChannelRegistry channels) => _channels = channels;

    public Task<bool> CanJoinAsync(
        UserId userId, string group, CancellationToken cancellationToken = default) =>
        _channels.CanJoinAsync(userId, group, cancellationToken);

    public Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default) =>
        _channels.CanObservePresenceAsync(observer, subject, cancellationToken);
}
