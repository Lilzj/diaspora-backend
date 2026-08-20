using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Housing;

/// <summary>
/// A landlord agreed a time to show a property.
/// <para>
/// <b>The event B11 explicitly wrote for this module</b> — its own summary says so. Two triggers:
/// the confirmation, and the reminder the rule schedules against the appointment, which is the
/// brief's "viewing reminder".
/// </para>
/// <para>
/// The address is not here and must not be added. B11 leaves it off deliberately: entitlement to the
/// full address is a decision made against a request with a viewer's identity attached, not a
/// payload on a bus. The notification carries a link, and the module that owns the address decides
/// what to show when the member follows it.
/// </para>
/// </summary>
public sealed class ViewingScheduledConsumer : ModuleEventConsumer<ViewingScheduledIntegrationEvent>
{
    public ViewingScheduledConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Housing";

    protected override IEnumerable<NotificationTrigger> Translate(
        ViewingScheduledIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["scheduledFor"] = integrationEvent.ScheduledFor;
        values["durationMinutes"] = integrationEvent.DurationMinutes;
        values["isVirtual"] = integrationEvent.IsVirtual;
        values["listingId"] = integrationEvent.ListingId;

        var seeker = UserId.From(integrationEvent.SeekerUserId);

        yield return Trigger(
            seeker,
            "seeker",
            integrationEvent.ScheduledAt,
            values,
            subjectType: "viewing",
            subjectId: integrationEvent.ViewingId.ToString(),
            subjectMoment: integrationEvent.ScheduledFor);

        yield return Trigger(
            seeker,
            "seeker-reminder",
            integrationEvent.ScheduledAt,
            values,
            subjectType: "viewing",
            subjectId: integrationEvent.ViewingId.ToString(),
            subjectMoment: integrationEvent.ScheduledFor);
    }
}
