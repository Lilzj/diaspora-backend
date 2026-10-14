using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Events;

namespace Diaspora.Modules.Housing.Application.EventHandlers;

/// <summary>Tells the rest of the platform a home listing was deleted (14 Oct 2026).</summary>
public sealed class HousingListingDeletedRelayEventHandler
    : DomainEventHandler<HousingListingDeletedDomainEvent>
{
    private readonly HousingIntegrationEventRelay _relay;

    public HousingListingDeletedRelayEventHandler(HousingIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        HousingListingDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
