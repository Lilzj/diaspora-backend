using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A channel gave up.
/// <para>
/// Logged at warning rather than error, and only on the final failure. A retry is not news, and a
/// dead push token is an ordinary fact about a member who changed phones — an error level here would
/// make the log unreadable on any platform with real users.
/// </para>
/// </summary>
public sealed class NotificationFailedEventHandler
    : DomainEventHandler<NotificationFailedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly INotificationRepository _notifications;
    private readonly NotificationAnalyticsRecorder _analytics;
    private readonly ILogger<NotificationFailedEventHandler> _logger;

    public NotificationFailedEventHandler(
        NotificationIntegrationEventRelay relay,
        INotificationRepository notifications,
        NotificationAnalyticsRecorder analytics,
        ILogger<NotificationFailedEventHandler> logger)
    {
        _relay = relay;
        _notifications = notifications;
        _analytics = analytics;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        NotificationFailedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        var notification = await _notifications.GetAsync(domainEvent.NotificationId, cancellationToken);

        if (notification is not null)
        {
            await _analytics.RecordFailedAsync(
                domainEvent.CategoryKey,
                domainEvent.Channel,
                notification.TemplateKey,
                domainEvent.Attempts,
                cancellationToken);
        }

        _logger.LogWarning(
            "Notification {NotificationId} failed on {Channel} after {Attempts} attempt(s): {ReasonCode}.",
            domainEvent.NotificationId.Value,
            domainEvent.Channel,
            domainEvent.Attempts,
            domainEvent.ReasonCode);
    }
}
