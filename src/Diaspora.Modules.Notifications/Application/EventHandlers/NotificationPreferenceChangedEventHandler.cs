using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Domain.Events;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A member changed how they want to be contacted: publish it.
/// <para>
/// <b>One published event per section change, and that is a deliberate simplification of something
/// the relay would rather collapse.</b> Collapsing four switch changes on one screen into a single
/// published fact would need state across a dispatch loop, and the loop deliberately has none. A
/// consumer counting opt-outs keys on <c>EventId</c> for idempotency and on
/// <c>IsFullyOptedOut</c> for the signal it wants, so several events describing the same member's
/// settings settle to the same answer.
/// </para>
/// <para>
/// The published contract carries which sections moved and never the settings themselves — a
/// member's quiet hours say when they sleep and their muted categories say what they are avoiding.
/// </para>
/// </summary>
public sealed class NotificationPreferenceChangedEventHandler
    : DomainEventHandler<NotificationPreferenceChangedDomainEvent>
{
    private readonly NotificationIntegrationEventRelay _relay;

    public NotificationPreferenceChangedEventHandler(NotificationIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        NotificationPreferenceChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(
            domainEvent.UserId.Value,
            [domainEvent.Section],
            domainEvent.IsFullyOptedOut,
            domainEvent.OccurredAt,
            cancellationToken);
}
