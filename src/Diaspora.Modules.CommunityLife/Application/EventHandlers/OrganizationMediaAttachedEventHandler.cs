using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

public sealed class OrganizationMediaAttachedEventHandler
    : DomainEventHandler<OrganizationMediaAttachedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public OrganizationMediaAttachedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        OrganizationMediaAttachedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
