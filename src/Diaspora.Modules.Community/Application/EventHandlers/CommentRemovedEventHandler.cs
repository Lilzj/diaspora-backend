using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a comment removal.</summary>
public sealed class CommentRemovedEventHandler : DomainEventHandler<CommentRemovedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public CommentRemovedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommentRemovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityCommentRemovedIntegrationEvent
            {
                CommentId = domainEvent.CommentId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                ByModerator = domainEvent.ByModerator
            },
            cancellationToken);
    }
}
