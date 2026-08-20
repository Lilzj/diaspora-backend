using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Observability;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A member followed the message somewhere.
/// <para>
/// The badge is not touched here. A click always arrives with an open — the aggregate backfills one
/// when a member taps straight through from a push banner — so the counter has already moved, and
/// dropping it twice would be the most common way an unread count goes wrong.
/// </para>
/// </summary>
public sealed class NotificationClickedEventHandler
    : DomainEventHandler<NotificationClickedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationClickedEventHandler(
        NotificationIntegrationEventRelay relay, NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationClickedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _analytics.RecordClickedAsync(
            domainEvent.CategoryKey,
            NotificationChannelKind.InApp,
            domainEvent.TemplateKey,
            cancellationToken);

        NotificationTelemetry.Clicked.Add(
            1, new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));
    }
}
