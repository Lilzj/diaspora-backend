using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Observability;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a reaction, carrying its key and sentiment as strings.</summary>
public sealed class ReactionAddedEventHandler : DomainEventHandler<ReactionAddedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public ReactionAddedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ReactionAddedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CommunityTelemetry.ReactionsRecorded.Add(
            1,
            new KeyValuePair<string, object?>("reaction", domainEvent.ReactionKey),
            new KeyValuePair<string, object?>(
                "sentiment", CommunityWireNames.ToWire(domainEvent.Sentiment)));

        return _relay.PublishAsync(
            new CommunityReactionAddedIntegrationEvent
            {
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                UserId = domainEvent.UserId.Value,
                ReactionKey = domainEvent.ReactionKey,
                Sentiment = CommunityWireNames.ToWire(domainEvent.Sentiment),
                ContentAuthorId = domainEvent.ContentAuthorId,
                CountryCode = domainEvent.CountryCode
            },
            cancellationToken);
    }
}
