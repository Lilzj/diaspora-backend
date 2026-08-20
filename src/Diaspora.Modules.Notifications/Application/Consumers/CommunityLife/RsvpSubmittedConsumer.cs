using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;

/// <summary>
/// Somebody said they are coming.
/// <para>
/// The attendee is told, because a waitlisted response is news they would otherwise discover at the
/// door. <b>The organizer is told too, since B13.1.</b> The contract used to carry the attendee's
/// member and not the organizer's, so this module could confirm an RSVP to the person who made it
/// and could not tell the person running the hall — and reaching into B12 for the answer would have
/// been exactly the coupling this module exists without.
/// </para>
/// <para>
/// <b>The organizer's message is one per response and the rule throttles it, not this code.</b> An
/// event with three hundred attendees produces three hundred of these facts; whether they arrive as
/// three hundred messages or one digest is a routing decision, and a consumer that decided it here
/// would be making a rule out of a hard-coded opinion.
/// </para>
/// </summary>
public sealed class RsvpSubmittedConsumer : ModuleEventConsumer<RsvpSubmittedIntegrationEvent>
{
    public RsvpSubmittedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        RsvpSubmittedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["status"] = integrationEvent.Status;
        values["partySize"] = integrationEvent.PartySize;
        values["goingCount"] = integrationEvent.GoingCount;
        values["remainingPlaces"] = integrationEvent.RemainingPlaces ?? 0;
        values["eventId"] = integrationEvent.CommunityEventId;

        yield return Trigger(
            UserId.From(integrationEvent.AttendeeUserId),
            "attendee",
            integrationEvent.RespondedAt,
            values,
            subjectType: "event",
            subjectId: integrationEvent.CommunityEventId.ToString(),
            count: integrationEvent.PartySize);

        // B13.3. The reminder, on its own audience and only when the contract can support one.
        //
        // Two reasons it is not simply a second rule on "attendee". A scheduled rule with no subject
        // moment sends immediately by the dispatcher's documented rule, so a contract published
        // before the enrichment would have reminded somebody about an event the instant they said
        // they were coming. And the reminder's wording needs the event's name, which the same
        // contracts do not carry — a required variable the handler cannot supply is a logged render
        // failure on every RSVP in the backlog. Yielding nothing is the honest answer to both: an
        // unenriched response simply produces no reminder.
        if (integrationEvent is { EventStartsAt: { } startsAt, EventTitle: { } title })
        {
            var reminderValues = Values();
            reminderValues["title"] = title;
            reminderValues["startsAt"] = startsAt;
            reminderValues["eventId"] = integrationEvent.CommunityEventId;

            yield return Trigger(
                UserId.From(integrationEvent.AttendeeUserId),
                "attendee-reminder",
                integrationEvent.RespondedAt,
                reminderValues,
                subjectType: "event",
                subjectId: integrationEvent.CommunityEventId.ToString(),

                // What the rule's offset is applied to. "One day before" means one day before this.
                subjectMoment: startsAt);
        }

        // Null before B13.1, and null when the event was gone by the time B12 read it back.
        // An organizer who responded to their own event does not need telling about it.
        if (integrationEvent.OrganizerUserId is not { } organizerUserId
            || organizerUserId == integrationEvent.AttendeeUserId)
        {
            yield break;
        }

        yield return Trigger(
            UserId.From(organizerUserId),
            "organizer",
            integrationEvent.RespondedAt,
            values,
            subjectType: "event",
            subjectId: integrationEvent.CommunityEventId.ToString(),
            count: integrationEvent.PartySize);
    }
}
