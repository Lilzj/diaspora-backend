using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// A member owns the channel named after them, and observes their own availability.
/// <para>
/// <b>The one subscription that needs no module to vouch for it.</b> The channel name is derived from
/// the caller's authenticated subject, so a join is a request to receive their own notifications on
/// their own devices — every notification this platform pushes travels on it, and the hub joins it
/// server-side on connect rather than waiting to be asked.
/// </para>
/// <para>
/// <b>This is what the shipped <c>OwnGroupRealtimeGroupAuthorizer</c> used to be, moved onto the
/// prefix seam.</b> The old class answered for every prefix and refused the ones it did not own,
/// which is why a second module could only participate by replacing it. Claiming <c>user:</c> and
/// nothing else says the same thing and leaves room beside it.
/// </para>
/// </summary>
public sealed class UserRealtimeChannelAuthorizer : IRealtimeChannelAuthorizer
{
    public string Module => NotificationsModuleMetadata.Name;

    public string ChannelPrefix => RealtimeChannels.UserPrefix;

    public Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Parsed rather than compared as a string. A client supplies this, and "starts with user:"
        // is satisfied by "user:" followed by anything at all.
        var owner = RealtimeChannels.IdentifierOf(channel, RealtimeChannels.UserPrefix);

        return Task.FromResult(owner is { } value && value == userId.Value);
    }

    /// <summary>
    /// The member the channel names, and nobody else.
    /// <para>
    /// Answered rather than left to the default so that a notification addressed to a member's own
    /// channel resolves through the same path as one addressed to an organization's — the fan-out
    /// has one shape, and the member's own channel is not a special case inside it.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<UserId>> ListSubscribersAsync(
        string channel, int limit, CancellationToken cancellationToken = default)
    {
        var owner = RealtimeChannels.IdentifierOf(channel, RealtimeChannels.UserPrefix);

        return Task.FromResult<IReadOnlyList<UserId>>(
            owner is { } value && limit > 0 ? [UserId.From(value)] : []);
    }

    /// <summary>
    /// Your own availability across your own devices. Always permitted; it is what the runtime
    /// broadcasts on connect and disconnect.
    /// </summary>
    public Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ArgumentNullException.ThrowIfNull(subject);

        return Task.FromResult(observer.Value == subject.Value);
    }
}
