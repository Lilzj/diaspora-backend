using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Housing;

/// <summary>
/// A housing reservation ended.
/// <para>
/// Named with the <c>Housing</c> prefix because Marketplace publishes a contract with the same type
/// name, and two consumers called <c>ReservationCancelledConsumer</c> in one assembly is a
/// disambiguation somebody would have to make at three in the morning.
/// </para>
/// <para>
/// The outcome travels as a value: a reservation the seeker withdrew, one the landlord released and
/// one that simply expired are three different messages, and only the template should decide which.
/// </para>
/// </summary>
public sealed class HousingReservationCancelledConsumer
    : ModuleEventConsumer<ReservationCancelledIntegrationEvent>
{
    public HousingReservationCancelledConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Housing";

    protected override IEnumerable<NotificationTrigger> Translate(
        ReservationCancelledIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["outcome"] = integrationEvent.Outcome;
        values["units"] = integrationEvent.Units;
        values["reason"] = integrationEvent.Reason ?? string.Empty;
        values["listingId"] = integrationEvent.ListingId;

        yield return Trigger(
            UserId.From(integrationEvent.SeekerUserId),
            "seeker",
            integrationEvent.CancelledAt,
            values,
            subjectType: "housing",
            subjectId: integrationEvent.ListingId.ToString());
    }
}
