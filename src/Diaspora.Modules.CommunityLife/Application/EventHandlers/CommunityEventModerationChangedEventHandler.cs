using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

public sealed class CommunityEventModerationChangedEventHandler
    : DomainEventHandler<CommunityEventModerationChangedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public CommunityEventModerationChangedEventHandler(
        CommunityLifeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommunityEventModerationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
