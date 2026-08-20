using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a mention. Only resolved mentions reach here.</summary>
public sealed class MentionCreatedEventHandler : DomainEventHandler<MentionCreatedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public MentionCreatedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        MentionCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityMentionCreatedIntegrationEvent
            {
                MentionId = domainEvent.MentionId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                MentionedBy = domainEvent.MentionedBy.Value,
                TargetType = CommunityWireNames.ToWire(domainEvent.TargetType),
                TargetId = domainEvent.TargetId
            },
            cancellationToken);
    }
}
