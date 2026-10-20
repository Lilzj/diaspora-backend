using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes an unfollow.
/// <para>
/// Still not notified to anybody (see <see cref="FollowRemovedDomainEvent"/>). Published so the
/// tallies other modules keep from <see cref="CommunityFollowCreatedIntegrationEvent"/> can come
/// down as well as go up.
/// </para>
/// </summary>
public sealed class FollowRemovedEventHandler : DomainEventHandler<FollowRemovedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public FollowRemovedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        FollowRemovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityFollowRemovedIntegrationEvent
            {
                FollowerId = domainEvent.FollowerId.Value,
                TargetType = CommunityWireNames.ToWire(domainEvent.TargetType),
                TargetId = domainEvent.TargetId
            },
            cancellationToken);
    }
}
