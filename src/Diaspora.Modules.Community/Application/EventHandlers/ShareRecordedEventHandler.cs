using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a share.</summary>
public sealed class ShareRecordedEventHandler : DomainEventHandler<ShareRecordedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public ShareRecordedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ShareRecordedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityShareRecordedIntegrationEvent
            {
                ShareId = domainEvent.ShareId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                SharedBy = domainEvent.SharedBy.Value,
                Kind = CommunityWireNames.ToWire(domainEvent.Kind),
                ContentAuthorId = domainEvent.ContentAuthorId,
                RecipientId = domainEvent.RecipientId
            },
            cancellationToken);
    }
}
