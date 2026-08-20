using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;

/// <summary>
/// Somebody arrived at an event.
/// <para>
/// Low urgency and digest-bound by rule: a check-in confirmation is a receipt, and a receipt that
/// interrupts somebody who is standing in the room it refers to is the clearest possible example of
/// a notification nobody asked for.
/// </para>
/// </summary>
public sealed class EventCheckInCompletedConsumer
    : ModuleEventConsumer<EventCheckInCompletedIntegrationEvent>
{
    public EventCheckInCompletedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        EventCheckInCompletedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["eventId"] = integrationEvent.CommunityEventId;

        yield return Trigger(
            UserId.From(integrationEvent.AttendeeUserId),
            "attendee",
            integrationEvent.OccurredAt,
            values,
            subjectType: "event",
            subjectId: integrationEvent.CommunityEventId.ToString());
    }
}
