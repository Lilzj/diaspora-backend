using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a follow.</summary>
public sealed class FollowCreatedEventHandler : DomainEventHandler<FollowCreatedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public FollowCreatedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        FollowCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityFollowCreatedIntegrationEvent
            {
                FollowerId = domainEvent.FollowerId.Value,
                TargetType = CommunityWireNames.ToWire(domainEvent.TargetType),
                TargetId = domainEvent.TargetId
            },
            cancellationToken);
    }
}
