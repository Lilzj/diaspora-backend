using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// A drafted event.
/// <para>
/// Reads the row back for its series membership, which the creation event does not carry: the
/// aggregate raises that event inside <c>Draft</c>, before the expansion service has attached it to
/// anything, so the fact does not exist yet at the moment the event is raised. Reading it here is a
/// hit on the identity map rather than a round trip, because the same unit of work created it.
/// </para>
/// </summary>
public sealed class CommunityEventCreatedEventHandler
    : DomainEventHandler<CommunityEventCreatedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly ICommunityEventRepository _events;

    public CommunityEventCreatedEventHandler(
        CommunityLifeIntegrationEventRelay relay, ICommunityEventRepository events)
    {
        _relay = relay;
        _events = events;
    }

    protected override async Task HandleAsync(
        CommunityEventCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent,
            communityEvent?.SeriesId?.Value,
            communityEvent?.SeriesSequence,
            cancellationToken);
    }
}
