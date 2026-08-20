using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Memberships;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Somebody came through the door: publish it, and credit the attendance to their membership.
/// <para>
/// <b>This handler deliberately settles nothing.</b> A check-in is an ordinary
/// going-to-checked-in transition, so the aggregate has already raised its status event and
/// <c>EventRsvpStatusChangedEventHandler</c> has already moved the counters. The aggregate raises
/// the two in that order and the dispatcher awaits them in turn, which is why the count read below
/// already includes this arrival.
/// </para>
/// <para>
/// <b>The membership credit is what makes a volunteer's record real.</b> Somebody who has been to
/// eleven of their association's events is a different member from somebody who joined and never
/// came, and that difference is only knowable if it is recorded at the door.
/// </para>
/// </summary>
public sealed class EventCheckInCompletedEventHandler
    : DomainEventHandler<EventCheckInCompletedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationMembershipRepository _memberships;

    public EventCheckInCompletedEventHandler(
        CommunityLifeIntegrationEventRelay relay,
        ICommunityEventRepository events,
        IOrganizationMembershipRepository memberships)
    {
        _relay = relay;
        _events = events;
        _memberships = memberships;
    }

    protected override async Task HandleAsync(
        EventCheckInCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        var communityEvent = await _events.GetAsync(
            domainEvent.CommunityEventId, cancellationToken);

        await _relay.PublishAsync(
            domainEvent,
            communityEvent?.Capacity.CheckedInCount ?? domainEvent.PartySize,
            cancellationToken);

        await CreditMembershipAsync(domainEvent, cancellationToken);
    }

    private async Task CreditMembershipAsync(
        EventCheckInCompletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.OrganizationId is not { } organizationId)
        {
            return;
        }

        var membership = await _memberships.FindCurrentAsync(
            organizationId, domainEvent.AttendeeUserId, cancellationToken);

        membership?.RecordAttendance(domainEvent.CheckedInAt);
    }
}
