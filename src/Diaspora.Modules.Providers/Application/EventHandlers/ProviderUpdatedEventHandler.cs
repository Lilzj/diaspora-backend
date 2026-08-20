using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>Relays an edit outward, carrying the field list a consumer branches on.</summary>
public sealed class ProviderUpdatedEventHandler : DomainEventHandler<ProviderUpdatedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderUpdatedEventHandler(ProviderIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ProviderUpdatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
