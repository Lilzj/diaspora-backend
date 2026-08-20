using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>Relays a new service outward, for followers to hear about.</summary>
public sealed class ProviderServiceAddedEventHandler
    : DomainEventHandler<ProviderServiceAddedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderServiceAddedEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderServiceAddedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
