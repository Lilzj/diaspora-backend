using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Notifications;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A message left the inbox.
/// <para>
/// <c>WasUnread</c> comes from the event rather than being read back, because by the time this runs
/// the aggregate has already changed and the fact the counter needs is gone. Every counter defect
/// this module could have had is a variation on reading that back too late.
/// </para>
/// </summary>
public sealed class NotificationArchivedEventHandler
    : DomainEventHandler<NotificationArchivedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly InboxCounterService _counters;
    private readonly INotificationRepository _notifications;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationArchivedEventHandler(
        NotificationIntegrationEventRelay relay,
        InboxCounterService counters,
        INotificationRepository notifications,
        NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _counters = counters;
        _notifications = notifications;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationArchivedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _counters.RecordArchivedAsync(
            domainEvent.RecipientUserId,
            domainEvent.CategoryKey,
            domainEvent.WasUnread,
            cancellationToken);

        var notification = await _notifications.GetAsync(domainEvent.NotificationId, cancellationToken);

        if (notification is null)
        {
            return;
        }

        await _analytics.RecordArchivedAsync(
            domainEvent.CategoryKey,
            notification.TemplateKey,
            domainEvent.Reason == ArchiveReason.Dismissed,
            cancellationToken);
    }
}
