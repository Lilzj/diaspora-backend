using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes that an insight is waiting for a moderator.
/// <para>
/// Carries no body: the text has not been reviewed, and a payload containing it would let any bus
/// subscriber publish an unchecked claim about a real residential area.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightSubmittedEventHandler
    : DomainEventHandler<NeighborhoodInsightSubmittedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public NeighborhoodInsightSubmittedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        NeighborhoodInsightSubmittedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
