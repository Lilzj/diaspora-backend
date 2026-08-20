using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Observability;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A member read the message: publish it, drop the badge, move the bucket.
/// <para>
/// The engagement is recorded against the channel the member was on, which is what makes "push is
/// delivering but nobody reads it" a question with an answer.
/// </para>
/// </summary>
public sealed class NotificationOpenedEventHandler
    : DomainEventHandler<NotificationOpenedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;
    private readonly InboxCounterService _counters;
    private readonly NotificationAnalyticsRecorder _analytics;

    public NotificationOpenedEventHandler(
        NotificationIntegrationEventRelay relay,
        InboxCounterService counters,
        NotificationAnalyticsRecorder analytics)
    {
        _relay = relay;
        _counters = counters;
        _analytics = analytics;
    }

    protected override async Task HandleAsync(
        NotificationOpenedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await _relay.PublishAsync(domainEvent, cancellationToken);

        await _counters.RecordReadAsync(
            domainEvent.RecipientUserId, domainEvent.CategoryKey, cancellationToken);

        await _analytics.RecordOpenedAsync(
            domainEvent.CategoryKey,
            ParseChannel(domainEvent.Channel),
            domainEvent.TemplateKey,
            domainEvent.MinutesToOpen,
            cancellationToken);

        NotificationTelemetry.Opened.Add(
            1, new KeyValuePair<string, object?>("category", domainEvent.CategoryKey));
    }

    /// <summary>
    /// The channel a client reported, or the inbox.
    /// <para>
    /// Clients send this as a string, and an unrecognised value must not lose the engagement — the
    /// inbox is the honest default, because that is where the member was if they were nowhere else.
    /// </para>
    /// </summary>
    private static NotificationChannelKind ParseChannel(string channel) =>
        Enum.TryParse<NotificationChannelKind>(channel, ignoreCase: true, out var parsed)
            ? parsed
            : NotificationChannelKind.InApp;
}
