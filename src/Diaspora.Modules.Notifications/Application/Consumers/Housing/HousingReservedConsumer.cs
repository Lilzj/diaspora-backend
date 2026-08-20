using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Housing;

/// <summary>A seeker reserved a room or a unit. The brief's "reservation confirmed".</summary>
public sealed class HousingReservedConsumer : ModuleEventConsumer<HousingReservedIntegrationEvent>
{
    public HousingReservedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Housing";

    protected override IEnumerable<NotificationTrigger> Translate(
        HousingReservedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["units"] = integrationEvent.Units;
        values["remainingUnits"] = integrationEvent.RemainingUnits;
        values["listingId"] = integrationEvent.ListingId;

        yield return Trigger(
            UserId.From(integrationEvent.SeekerUserId),
            "seeker",
            integrationEvent.ReservedAt,
            values,
            subjectType: "housing",
            subjectId: integrationEvent.ListingId.ToString(),
            count: integrationEvent.Units);
    }
}
