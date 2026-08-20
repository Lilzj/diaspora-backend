using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Housing;

/// <summary>
/// A landlord's verification state moved.
/// <para>
/// Both directions are published on one contract, so <c>isVerified</c> travels as a value and the
/// rule catalogue holds two rules over the same event — one for the grant and one for the loss. That
/// is the pattern for every "state changed" contract in the platform, and it is why the rule is
/// keyed on the event plus an audience rather than on the event alone.
/// </para>
/// </summary>
public sealed class LandlordVerifiedConsumer : ModuleEventConsumer<LandlordVerifiedIntegrationEvent>
{
    public LandlordVerifiedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Housing";

    protected override IEnumerable<NotificationTrigger> Translate(
        LandlordVerifiedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["landlordName"] = integrationEvent.LandlordName;
        values["landlordKind"] = integrationEvent.LandlordKind;
        values["isVerified"] = integrationEvent.IsVerified;
        values["badge"] = integrationEvent.Badge ?? string.Empty;
        values["previousState"] = integrationEvent.PreviousState;
        values["currentState"] = integrationEvent.CurrentState;

        yield return Trigger(
            UserId.From(integrationEvent.OwnerUserId),
            integrationEvent.IsVerified ? "landlord-verified" : "landlord-unverified",
            integrationEvent.ChangedAt,
            values,
            subjectType: "landlord",
            subjectId: integrationEvent.LandlordId.ToString());
    }
}
