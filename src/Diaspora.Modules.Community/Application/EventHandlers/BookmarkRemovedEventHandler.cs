using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes an unsave, so a count of saves can come down as well as go up.</summary>
public sealed class BookmarkRemovedEventHandler : DomainEventHandler<BookmarkRemovedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public BookmarkRemovedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        BookmarkRemovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityBookmarkRemovedIntegrationEvent
            {
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                UserId = domainEvent.UserId.Value
            },
            cancellationToken);
    }
}
