using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Observability;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A channel was asked to carry the message.
/// <para>
/// The template key is read back from the aggregate rather than carried on the event: a queued event
/// fires once per channel and would otherwise repeat a field that never varies within one message.
/// The read is inside the same unit of work, so it is the identity map rather than a round trip.
/// </para>
/// </summary>
public sealed class NotificationQueuedEventHandler
    : DomainEventHandler<NotificationQueuedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly INotificationRepository _notifications;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationQueuedEventHandler(
        NotificationIntegrationEventRelay relay,
        INotificationRepository notifications,
        NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _notifications = notifications;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationQueuedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        var notification = await _notifications.GetAsync(domainEvent.NotificationId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        await _analytics.RecordQueuedAsync(
            domainEvent.CategoryKey,
            domainEvent.Channel,
            notification.TemplateKey,
            cancellationToken);

        NotificationTelemetry.Queued.Add(
            1, new KeyValuePair<string, object?>("channel", domainEvent.Channel.ToString()));
    }
}
