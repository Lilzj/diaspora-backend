using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// A verification was withdrawn for cause.
/// <para>
/// Distinct from expiry, and the distinction is the member's dignity: expiring is administrative and
/// revocation is a judgement. Two events, two rules, two templates — collapsing them would tell
/// somebody whose permit simply ran out that the platform no longer trusts them.
/// </para>
/// </summary>
public sealed class VerificationRevokedConsumer
    : ModuleEventConsumer<VerificationRevokedIntegrationEvent>
{
    public VerificationRevokedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationRevokedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["reason"] = integrationEvent.Reason;
        values["revokedAt"] = integrationEvent.RevokedAt;
        values["reasontext"] = string.IsNullOrWhiteSpace(integrationEvent.Reason)
            ? string.Empty
            : $"The reason given: {integrationEvent.Reason.Trim().TrimEnd('.')}. ";

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.RevokedAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}
