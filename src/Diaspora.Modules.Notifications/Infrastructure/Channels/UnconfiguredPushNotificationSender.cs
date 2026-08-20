using Diaspora.Modules.Notifications.Extensibility.Channels;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The push transport that ships with this phase: it records the attempt and sends nothing.
/// <para>
/// <b>Registered rather than absent, and that distinction is the whole design.</b> With no
/// implementation the container would fail to resolve the push channel and the module would have to
/// special-case its own seam. With this one, the entire path above it runs on every deployment —
/// preferences narrow the channels, quiet hours defer, the throttle refuses, the delivery row is
/// written, the analytics bucket moves — and the only thing that does not happen is the network
/// call. Acquiring Firebase is then one registration line, exercising code that has been running in
/// production all along.
/// </para>
/// <para>
/// <see cref="IsConfigured"/> is false, so the dispatcher records <c>Unsupported</c> once instead of
/// failing eight times up a retry ladder. The log line is Debug: on a deployment with no push
/// provider this would otherwise be the loudest thing in the log.
/// </para>
/// </summary>
public sealed class UnconfiguredPushNotificationSender : IPushNotificationSender
{
    private readonly ILogger<UnconfiguredPushNotificationSender> _logger;

    public UnconfiguredPushNotificationSender(ILogger<UnconfiguredPushNotificationSender> logger) =>
        _logger = logger;

    public bool IsConfigured => false;

    public Task<PushSendResult> SendAsync(
        PushMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug(
            "No push provider is configured. Notification {NotificationId} was not pushed to {Count} endpoint(s).",
            message.NotificationId,
            message.Endpoints.Count);

        return Task.FromResult(PushSendResult.Failure(
            "NotConfigured", "No push provider is configured.", isPermanent: true));
    }
}
