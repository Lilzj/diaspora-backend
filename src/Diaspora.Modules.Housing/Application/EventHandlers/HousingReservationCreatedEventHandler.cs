using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>Publishes the reservation contract, carrying what is left after the hold.</summary>
public sealed class HousingReservationCreatedEventHandler
    : DomainEventHandler<HousingReservationCreatedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingReservationCreatedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingReservationCreatedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
