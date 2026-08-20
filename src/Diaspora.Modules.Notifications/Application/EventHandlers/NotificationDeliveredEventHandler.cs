using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Notifications;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>A channel succeeded: publish it and move the bucket.</summary>
public sealed class NotificationDeliveredEventHandler
    : DomainEventHandler<NotificationDeliveredDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly INotificationRepository _notifications;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationDeliveredEventHandler(
        NotificationIntegrationEventRelay relay,
        INotificationRepository notifications,
        NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _notifications = notifications;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationDeliveredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        var notification = await _notifications.GetAsync(domainEvent.NotificationId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        await _analytics.RecordDeliveredAsync(
            domainEvent.CategoryKey,
            domainEvent.Channel,
            notification.TemplateKey,
            domainEvent.Attempt,
            cancellationToken);
    }
}
