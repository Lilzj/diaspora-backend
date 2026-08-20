using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// Trust rejected a verification.
/// <para>
/// The reason code travels and the free-text detail does not. A rejection reason written by a
/// reviewer is for a reviewer; a member deserves a sentence the template author wrote, chosen by the
/// code, which is also the only way this message can be translated at all.
/// </para>
/// </summary>
public sealed class VerificationRejectedConsumer
    : ModuleEventConsumer<VerificationRejectedIntegrationEvent>
{
    public VerificationRejectedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationRejectedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["reasonCode"] = integrationEvent.ReasonCode;
        values["targetCountry"] = integrationEvent.TargetCountry ?? string.Empty;
        values["rejectedAt"] = integrationEvent.RejectedAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.RejectedAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}
