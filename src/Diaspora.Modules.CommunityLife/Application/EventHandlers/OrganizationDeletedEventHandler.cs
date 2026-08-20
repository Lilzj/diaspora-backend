using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>Publishes that a body is gone. Consumers de-index on it.</summary>
public sealed class OrganizationDeletedEventHandler
    : DomainEventHandler<OrganizationDeletedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public OrganizationDeletedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        OrganizationDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
