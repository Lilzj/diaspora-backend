using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>Relays a schedule change outward, so open-now projections can be recomputed.</summary>
public sealed class ProviderAvailabilityChangedEventHandler
    : DomainEventHandler<ProviderAvailabilityChangedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderAvailabilityChangedEventHandler(ProviderIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        ProviderAvailabilityChangedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
