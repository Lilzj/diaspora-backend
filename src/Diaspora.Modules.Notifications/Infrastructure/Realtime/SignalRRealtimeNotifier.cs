using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.SharedKernel.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// The one class in the platform that knows real-time is SignalR.
/// <para>
/// <b>Everything above <see cref="IRealtimeNotifier"/> is transport-free, and this is where that
/// stops.</b> The in-app channel, the presence broadcast, the typing relay and whatever module
/// eventually owns conversations all send through the port; swapping SignalR for a hosted real-time
/// service replaces this file and nothing else. An architecture test asserts that no other type
/// outside the hub itself references the SignalR namespace.
/// </para>
/// <para>
/// <b>A send never throws and never blocks a caller.</b> Real-time delivery is best-effort by
/// definition — the notification is already in the inbox, and push exists for the member whose phone
/// is in a tunnel. The first caller that forgot to wrap this would fail a notification because a
/// socket closed mid-write, so the swallow lives here rather than at seven call sites.
/// </para>
/// <para>
/// <b>Uses the untyped hub context deliberately.</b> The hub itself is
/// <c>Hub&lt;IRealtimeClient&gt;</c>, so every call it makes to a client is compiler-checked. Fan-out
/// from outside the hub dispatches by event name because the seam has to carry payloads this module
/// does not define — the conversation vertical sends <c>MessageReceived</c> with its own shape, and
/// a typed context would make that a change to this assembly.
/// </para>
/// </summary>
public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<RealtimeHub> _hub;
    private readonly IOptionsMonitor<RealtimeOptions> _options;
    private readonly ILogger<SignalRRealtimeNotifier> _logger;

    public SignalRRealtimeNotifier(
        IHubContext<RealtimeHub> hub,
        IOptionsMonitor<RealtimeOptions> options,
        ILogger<SignalRRealtimeNotifier> logger)
    {
        _hub = hub;
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.CurrentValue.Enabled;

    public Task SendToUserAsync(
        UserId userId, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // The user group rather than Clients.User: both reach every socket a member holds, and the
        // group is the one the authorizer already reasons about. Two ways to address a member is two
        // places a future access rule would have to be applied.
        return SendAsync(
            () => _hub.Clients.Group(RealtimeGroups.ForUser(userId))
                .SendAsync(eventName, payload, cancellationToken),
            eventName);
    }

    public Task SendToGroupAsync(
        string group, string eventName, object payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        return SendAsync(
            () => _hub.Clients.Group(group).SendAsync(eventName, payload, cancellationToken),
            eventName);
    }

    public Task SendToGroupExceptAsync(
        string group,
        string exceptConnectionId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        return SendAsync(
            () => _hub.Clients.GroupExcept(group, exceptConnectionId)
                .SendAsync(eventName, payload, cancellationToken),
            eventName);
    }

    private async Task SendAsync(Func<Task> send, string eventName)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            await send();

            NotificationTelemetry.RealtimeSent.Add(
                1, new KeyValuePair<string, object?>("event", eventName));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Debug rather than Warning. A socket closing mid-write is the ordinary end of every
            // connection, and logging it above Debug would make a healthy deployment's loudest
            // signal be members putting their phones down.
            _logger.LogDebug(
                exception, "Real-time fan-out of {Event} did not complete.", eventName);

            NotificationTelemetry.RealtimeDropped.Add(
                1, new KeyValuePair<string, object?>("event", eventName));
        }
    }
}
