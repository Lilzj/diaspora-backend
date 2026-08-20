using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Realtime;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The inbox.
/// <para>
/// <b>It writes nothing, and that is correct rather than a shortcut.</b> The message is already in
/// the member's inbox — writing it there is what creating a notification means — so the in-app
/// "delivery" is the acknowledgement that it is readable. Modelling it as a channel anyway is what
/// makes the rest of the module uniform: preferences, quiet hours, digests, analytics and the
/// delivery record all work the same way for the inbox as for push, and no code anywhere says
/// "unless it is in-app".
/// </para>
/// <para>
/// <b>What it does do is tell a client that is already listening.</b> Real-time is not a separate
/// destination a member chooses between; it is the difference between an inbox that updates now and
/// one that updates on the next poll. Doing it here rather than as a seventh channel kind is
/// explained at length on <see cref="RealtimeInboxBroadcaster"/>, and the short version is that a
/// routed real-time kind would be suppressed by every member's category preferences and deferred by
/// their quiet hours to a moment when their socket no longer exists.
/// </para>
/// <para>
/// <b>It is always available and never fails.</b> That is the guarantee the whole module rests on: a
/// member always has one channel that cannot be lost to a dead token, an unconfigured provider or a
/// bounced address — which is why the preference aggregate refuses to let anybody switch it off, and
/// why a real-time fan-out that failed must not change the outcome. The broadcaster swallows its own
/// transport errors for exactly that reason.
/// </para>
/// </summary>
public sealed class InAppNotificationChannel : INotificationChannel
{
    private readonly RealtimeInboxBroadcaster _broadcaster;

    public InAppNotificationChannel(RealtimeInboxBroadcaster broadcaster) => _broadcaster = broadcaster;

    public NotificationChannelKind Kind => NotificationChannelKind.InApp;

    public bool IsAvailable => true;

    public async Task<NotificationChannelOutcome> SendAsync(
        NotificationChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await _broadcaster.BroadcastArrivalAsync(request, cancellationToken);

        return NotificationChannelOutcome.Success();
    }
}
