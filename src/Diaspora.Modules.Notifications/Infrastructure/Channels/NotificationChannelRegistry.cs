using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Channels;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The channel for a kind, resolved once.
/// <para>
/// <b>Registered channels are scanned from every platform assembly, so a channel can ship in a
/// module that is not this one.</b> That is what makes the seam real: an SMS channel arriving with a
/// future messaging module needs no edit here.
/// </para>
/// <para>
/// <b>Two channels claiming one kind is a startup failure, not a race.</b> Silently picking the
/// first would mean a deployment that accidentally registered a second push implementation delivered
/// through whichever one the container happened to order first — a difference nobody would find
/// until members reported missing notifications.
/// </para>
/// </summary>
public sealed class NotificationChannelRegistry
{
    private readonly Dictionary<NotificationChannelKind, INotificationChannel> _channels;

    public NotificationChannelRegistry(IEnumerable<INotificationChannel> channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        _channels = [];

        foreach (var channel in channels)
        {
            if (!_channels.TryAdd(channel.Kind, channel))
            {
                throw new InvalidOperationException(
                    $"Two notification channels claim the kind '{channel.Kind}': " +
                    $"'{_channels[channel.Kind].GetType().FullName}' and " +
                    $"'{channel.GetType().FullName}'. Exactly one implementation may serve a kind.");
            }
        }
    }

    /// <summary>The channel for a kind, or null when nothing implements it. SMS today.</summary>
    public INotificationChannel? Find(NotificationChannelKind kind) =>
        _channels.GetValueOrDefault(kind);

    /// <summary>Whether a kind can be delivered on right now — implemented and configured.</summary>
    public bool IsDeliverable(NotificationChannelKind kind) =>
        Find(kind) is { IsAvailable: true };

    /// <summary>Every kind with an implementation, whether or not it is configured.</summary>
    public IReadOnlyCollection<NotificationChannelKind> ImplementedKinds => _channels.Keys;
}
