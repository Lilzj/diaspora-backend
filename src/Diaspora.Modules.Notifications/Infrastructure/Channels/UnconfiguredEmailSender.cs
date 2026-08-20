using Diaspora.Modules.Notifications.Extensibility.Channels;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Channels;

/// <summary>
/// The email transport that ships with this phase: it records the attempt and sends nothing.
/// <para>
/// The twin of <see cref="UnconfiguredPushNotificationSender"/>, and for the same reason. See that
/// type for why an unconfigured implementation is registered rather than omitted.
/// </para>
/// </summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    private readonly ILogger<UnconfiguredEmailSender> _logger;

    public UnconfiguredEmailSender(ILogger<UnconfiguredEmailSender> logger) => _logger = logger;

    public bool IsConfigured => false;

    public Task<EmailSendResult> SendAsync(
        EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        _logger.LogDebug(
            "No email provider is configured. Notification {NotificationId} was not emailed.",
            message.NotificationId);

        return Task.FromResult(EmailSendResult.Failure(
            "NotConfigured", "No email provider is configured.", isPermanent: true));
    }
}
