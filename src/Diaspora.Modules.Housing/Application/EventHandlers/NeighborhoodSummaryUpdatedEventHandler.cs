using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>Publishes that a neighbourhood profile moved. Consumers re-read rather than diff.</summary>
public sealed class NeighborhoodSummaryUpdatedEventHandler
    : DomainEventHandler<NeighborhoodSummaryUpdatedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public NeighborhoodSummaryUpdatedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        NeighborhoodSummaryUpdatedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
