using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Policies;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// A response moving. <b>The single settlement path for an event's attendance counters</b> — every
/// place taken, released and used in the module passes through here, which is what makes the numbers
/// reconcilable: there is one file to read to know how a place can move.
/// <para>
/// <b>A check-in settles here too, not in the check-in handler.</b> Checking somebody in moves them
/// from going to checked-in through the ordinary transition, so the aggregate raises the status
/// event first and its own check-in event second. Settling in both places would count every arrival
/// twice.
/// </para>
/// <para>
/// <b>Promotion off the waiting list is not this handler's job.</b> It happens in the command that
/// released the place, inside the same unit of work, because a place that is free for the length of
/// a dispatch is a place two people can be told they have.
/// </para>
/// </summary>
public sealed class EventRsvpStatusChangedEventHandler
    : DomainEventHandler<EventRsvpStatusChangedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly ICommunityEventRepository _events;

    public EventRsvpStatusChangedEventHandler(
        CommunityLifeIntegrationEventRelay relay, ICommunityEventRepository events)
    {
        _relay = relay;
        _events = events;
    }

    protected override async Task HandleAsync(
        EventRsvpStatusChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        if (communityEvent is not null)
        {
            var wasFull = communityEvent.IsFull;

            var movement = RsvpStatusTransitions.Movement(
                domainEvent.PreviousStatus,
                domainEvent.CurrentStatus,
                domainEvent.PartySize);

            if (!movement.IsEmpty)
            {
                communityEvent.SettleAttendance(
                    domainEvent.ChangedAt,
                    movement.Going,
                    movement.Interested,
                    movement.Waitlisted,
                    movement.CheckedIn);
            }

            AttendanceMetrics.RecordTimeToFill(communityEvent, wasFull, domainEvent.ChangedAt);
        }

        await _relay.PublishAsync(
            domainEvent,
            communityEvent?.Schedule.StartsAt ?? domainEvent.ChangedAt,
            communityEvent?.Capacity.GoingCount ?? 0,
            communityEvent?.Capacity.RemainingPlaces,

            // B13.1. The organizer travels on the published contract so Communication can tell
            // them somebody is coming without asking this module who they are. Read off the
            // aggregate this handler already loaded, because this path runs on every response.
            communityEvent?.OrganizerUserId.Value,

            // B13.3. The relay reads the start and the title off it for the reminder contract.
            // Null when the event is gone, which is what stops a reminder being scheduled against
            // a moment that does not exist.
            communityEvent,
            cancellationToken);
    }
}
