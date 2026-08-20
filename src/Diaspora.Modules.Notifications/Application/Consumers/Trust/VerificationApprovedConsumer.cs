using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// Trust approved a verification.
/// <para>
/// <b>The message members most want and the one this module must be most careful with.</b> Trust
/// owns whether somebody is verified; this consumer owns nothing but the sentence about it, and the
/// only fields it reads are the ones the published contract carries. A partial approval says so,
/// because "verified" and "verified for one country" are different facts and telling somebody the
/// first when the second is true is how a member discovers the difference at a border.
/// </para>
/// </summary>
public sealed class VerificationApprovedConsumer
    : ModuleEventConsumer<VerificationApprovedIntegrationEvent>
{
    public VerificationApprovedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationApprovedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["targetCountry"] = integrationEvent.TargetCountry ?? string.Empty;
        values["isPartial"] = integrationEvent.Partial;
        values["approvedAt"] = integrationEvent.ApprovedAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.ApprovedAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}
