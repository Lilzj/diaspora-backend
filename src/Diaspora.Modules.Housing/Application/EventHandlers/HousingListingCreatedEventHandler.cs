using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>Publishes the creation contract. Audit and counting only - a draft is not on the market.</summary>
public sealed class HousingListingCreatedEventHandler
    : DomainEventHandler<HousingListingCreatedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingListingCreatedEventHandler(HousingIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        HousingListingCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
