using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>Tells a member what the reviewer needs before deciding their verification (7 Nov 2026).</summary>
public sealed class VerificationMoreInformationConsumer
    : ModuleEventConsumer<VerificationMoreInformationRequestedIntegrationEvent>
{
    public VerificationMoreInformationConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationMoreInformationRequestedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["details"] = integrationEvent.Details.Trim().TrimEnd('.') + ".";

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.RequestedAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}
