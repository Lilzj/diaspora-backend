using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Observability;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a comment. The event a notification module fans out from.</summary>
public sealed class CommentAddedEventHandler : DomainEventHandler<CommentAddedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public CommentAddedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        CommentAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CommunityTelemetry.CommentsAdded.Add(
            1,
            new KeyValuePair<string, object?>(
                "contentType", CommunityWireNames.ToWire(domainEvent.Content.Type)));

        return _relay.PublishAsync(
            new CommunityCommentAddedIntegrationEvent
            {
                CommentId = domainEvent.CommentId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                AuthorId = domainEvent.AuthorId.Value,
                ParentCommentId = domainEvent.ParentCommentId?.Value,
                ContentAuthorId = domainEvent.ContentAuthorId,
                ParentAuthorId = domainEvent.ParentAuthorId,
                Depth = domainEvent.Depth
            },
            cancellationToken);
    }
}
