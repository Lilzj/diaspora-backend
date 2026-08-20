using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

public sealed class OrganizationCreatedEventHandler
    : DomainEventHandler<OrganizationCreatedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public OrganizationCreatedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        OrganizationCreatedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
