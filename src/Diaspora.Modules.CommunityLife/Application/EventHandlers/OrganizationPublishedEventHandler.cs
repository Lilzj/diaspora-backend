using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

public sealed class OrganizationPublishedEventHandler
    : DomainEventHandler<OrganizationPublishedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public OrganizationPublishedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        OrganizationPublishedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
