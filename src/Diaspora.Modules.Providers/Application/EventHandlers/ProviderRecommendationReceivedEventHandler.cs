using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Domain.Events;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>Relays a recommendation outward.</summary>
public sealed class ProviderRecommendationReceivedEventHandler
    : DomainEventHandler<ProviderRecommendationReceivedDomainEvent>
{
    private readonly ProviderIntegrationEventRelay _relay;

    public ProviderRecommendationReceivedEventHandler(ProviderIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        ProviderRecommendationReceivedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
