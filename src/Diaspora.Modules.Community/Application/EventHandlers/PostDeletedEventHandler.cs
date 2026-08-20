using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a removal, distinguishing an author's delete from a moderator's.</summary>
public sealed class PostDeletedEventHandler : DomainEventHandler<PostDeletedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public PostDeletedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        PostDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityPostRemovedIntegrationEvent
            {
                PostId = domainEvent.PostId.Value,
                AuthorId = domainEvent.AuthorId.Value,
                Reason = domainEvent.ByModerator ? "removed" : "deleted"
            },
            cancellationToken);
    }
}
