using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// A first response to an event. Records the engagement, and nothing else.
/// <para>
/// <b>It deliberately neither settles a counter nor publishes anything</b>, and that is the whole
/// content of this class. Responding raises this event <em>and</em> a status change whose previous
/// status is a synthetic <c>Cancelled</c> — the aggregate says so in as many words — so the places
/// are taken and the contract is published by <c>EventRsvpStatusChangedEventHandler</c>. Doing
/// either here as well would count every first commitment twice: an event with forty responses would
/// report eighty places gone and refuse the forty-first member a seat that existed.
/// </para>
/// <para>
/// <b>What is genuinely only knowable here is that a response was made at all.</b> A member moving
/// between interested and going later is the same response changing its mind, so counting it again
/// would inflate the conversion rate an organizer reads to decide whether promoting the event
/// worked.
/// </para>
/// </summary>
public sealed class EventRsvpCreatedEventHandler
    : DomainEventHandler<EventRsvpCreatedDomainEvent>
{
    private readonly ICommunityEventRepository _events;

    public EventRsvpCreatedEventHandler(ICommunityEventRepository events) => _events = events;

    protected override async Task HandleAsync(
        EventRsvpCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        communityEvent?.RecordRsvp();
    }
}
