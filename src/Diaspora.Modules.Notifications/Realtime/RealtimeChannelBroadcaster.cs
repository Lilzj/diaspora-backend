using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// Announces that a record changed, to whoever is entitled to be listening.
/// <para>
/// <b>This is the whole of what a vertical needs from real-time, and it is one call.</b> A consumer
/// translating another module's published fact does not open a socket, name a transport, compose a
/// channel name or handle a failure — it says which record changed and how, and this decides where
/// that goes.
/// </para>
/// <para>
/// <b>Nothing here can fail a consumed event.</b> Every method swallows, because a live update is an
/// optimisation on top of a fact already committed and already in an inbox: the notification exists,
/// the REST resource is correct, and the client refetches on focus regardless. A broadcaster that
/// threw would dead-letter an integration event — and therefore stop every other consumer of it —
/// because somebody's phone was in a tunnel.
/// </para>
/// <para>
/// <b>A channel nobody claimed is not written to.</b> Sending to an unowned prefix would be a frame
/// no client could ever have joined to receive, which costs nothing and hides a real
/// misconfiguration: a module broadcasting to a family it forgot to authorise looks identical to one
/// whose members are all offline. The check makes the first one visible.
/// </para>
/// </summary>
public sealed class RealtimeChannelBroadcaster
{
    private readonly IRealtimeNotifier _notifier;
    private readonly RealtimeChannelRegistry _channels;
    private readonly ILogger<RealtimeChannelBroadcaster> _logger;

    public RealtimeChannelBroadcaster(
        IRealtimeNotifier notifier,
        RealtimeChannelRegistry channels,
        ILogger<RealtimeChannelBroadcaster> logger)
    {
        _notifier = notifier;
        _channels = channels;
        _logger = logger;
    }

    /// <summary>Tells a channel's listeners that a record changed.</summary>
    public Task ResourceChangedAsync(
        string channel, ResourceChanged change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (!_notifier.IsEnabled || string.IsNullOrWhiteSpace(channel))
        {
            return Task.CompletedTask;
        }

        var prefix = RealtimeChannels.PrefixOf(channel);

        if (prefix is null || !_channels.IsOwned(prefix))
        {
            _logger.LogWarning(
                "Nothing broadcast to '{Channel}': no module authorises the '{Prefix}' family, so no "
                + "client could have joined it.",
                channel,
                prefix ?? "(none)");

            return Task.CompletedTask;
        }

        return SafelyAsync(
            () => _notifier.SendToGroupAsync(
                channel, RealtimeEvents.ResourceChanged, change, cancellationToken),
            change.ChangeKind);
    }

    /// <summary>
    /// Tells one member that a record changed, on their own channel.
    /// <para>
    /// For a change that concerns a person rather than a subject — their membership decision, their
    /// payment — where broadcasting to the subject's channel would tell everybody else about it.
    /// </para>
    /// </summary>
    public Task ResourceChangedForUserAsync(
        UserId userId, ResourceChanged change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(change);

        return _notifier.IsEnabled
            ? SafelyAsync(
                () => _notifier.SendToUserAsync(
                    userId, RealtimeEvents.ResourceChanged, change, cancellationToken),
                change.ChangeKind)
            : Task.CompletedTask;
    }

    private async Task SafelyAsync(Func<Task> broadcast, string changeKind)
    {
        try
        {
            await broadcast();

            NotificationTelemetry.RealtimeSent.Add(
                1,
                new KeyValuePair<string, object?>("event", RealtimeEvents.ResourceChanged),
                new KeyValuePair<string, object?>("change", changeKind));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A live update is best-effort by definition. See the class summary.
        catch (Exception exception)
        {
            NotificationTelemetry.RealtimeDropped.Add(
                1, new KeyValuePair<string, object?>("event", RealtimeEvents.ResourceChanged));

            _logger.LogWarning(
                exception,
                "A resource-change broadcast failed. The change is committed and clients will see it "
                + "on their next fetch.");
        }
#pragma warning restore CA1031
    }
}
