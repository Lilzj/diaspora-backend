using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>
/// Publishes a reservation ending without anybody moving in.
/// <para>
/// Occupation is deliberately not published here: the listing own occupied event reports it, and
/// publishing both would count one letting twice in every consumer metrics.
/// </para>
/// </summary>
public sealed class HousingReservationStatusChangedEventHandler
    : DomainEventHandler<HousingReservationStatusChangedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingReservationStatusChangedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingReservationStatusChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
