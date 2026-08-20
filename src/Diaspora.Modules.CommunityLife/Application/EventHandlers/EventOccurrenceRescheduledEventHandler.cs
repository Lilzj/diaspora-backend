using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// One date of a repeating event moved off its generated slot.
/// <para>
/// Publishes in addition to the ordinary update the same act also raises, not instead of it: a
/// consumer that knows nothing about series still needs to hear that this event moved, and one that
/// does needs to hear that the pattern did not.
/// </para>
/// </summary>
public sealed class EventOccurrenceRescheduledEventHandler
    : DomainEventHandler<EventOccurrenceRescheduledDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public EventOccurrenceRescheduledEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        EventOccurrenceRescheduledDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
