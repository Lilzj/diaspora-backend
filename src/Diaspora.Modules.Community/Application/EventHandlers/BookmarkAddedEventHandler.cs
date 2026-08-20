using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a save. The strongest intent signal a recommender gets.</summary>
public sealed class BookmarkAddedEventHandler : DomainEventHandler<BookmarkAddedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public BookmarkAddedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        BookmarkAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityBookmarkAddedIntegrationEvent
            {
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                UserId = domainEvent.UserId.Value,
                ContentAuthorId = domainEvent.ContentAuthorId
            },
            cancellationToken);
    }
}
