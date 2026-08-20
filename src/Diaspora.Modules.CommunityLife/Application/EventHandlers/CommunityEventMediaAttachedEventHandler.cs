using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

public sealed class CommunityEventMediaAttachedEventHandler
    : DomainEventHandler<CommunityEventMediaAttachedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityEventMediaAttachedEventHandler(CommunityLifeIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        CommunityEventMediaAttachedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
