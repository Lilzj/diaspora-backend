using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A message was addressed to a member: publish it, move the badge, open the bucket.
/// <para>
/// <b>The counter is settled here rather than in the dispatcher, and inside the same unit of
/// work.</b> The badge and the message commit together or not at all — a member whose badge says one
/// and whose inbox shows nothing has stopped trusting both.
/// </para>
/// </summary>
public sealed class NotificationCreatedEventHandler
    : DomainEventHandler<NotificationCreatedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly InboxCounterService _counters;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationCreatedEventHandler(
        NotificationIntegrationEventRelay relay,
        InboxCounterService counters,
        NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _counters = counters;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _counters.RecordArrivalAsync(
            domainEvent.RecipientUserId,
            domainEvent.CategoryKey,
            domainEvent.CreatedAt,
            cancellationToken);

        await _analytics.RecordCreatedAsync(
            domainEvent.CategoryKey, domainEvent.TemplateKey, cancellationToken);
    }
}
