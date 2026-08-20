using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// A verification lapsed because its evidence expired.
/// <para>
/// Time-sensitive in a way an approval is not: a member whose residence permit verification has
/// lapsed loses capabilities they were using yesterday, and the message exists so they can act
/// before they discover it by being refused something.
/// </para>
/// </summary>
public sealed class VerificationExpiredConsumer
    : ModuleEventConsumer<VerificationExpiredIntegrationEvent>
{
    public VerificationExpiredConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationExpiredIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["targetCountry"] = integrationEvent.TargetCountry ?? string.Empty;
        values["expiredAt"] = integrationEvent.ExpiredAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.ExpiredAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}
