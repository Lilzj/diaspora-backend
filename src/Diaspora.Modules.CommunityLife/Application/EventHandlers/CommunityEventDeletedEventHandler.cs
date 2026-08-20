using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>Publishes that an event is gone. Consumers de-index on it.</summary>
public sealed class CommunityEventDeletedEventHandler
    : DomainEventHandler<CommunityEventDeletedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityEventDeletedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CommunityEventDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
