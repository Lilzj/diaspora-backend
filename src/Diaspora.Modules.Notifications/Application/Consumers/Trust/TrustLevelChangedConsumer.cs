using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// A member's trust level moved.
/// <para>
/// Both directions produce a trigger and the rule catalogue decides what each becomes — a rise is
/// worth celebrating and a fall is worth explaining, and both are things a member finds out about
/// eventually whether the platform tells them or not. Which is why the direction is a value the
/// template can switch on rather than two consumers.
/// </para>
/// </summary>
public sealed class TrustLevelChangedConsumer
    : ModuleEventConsumer<TrustLevelChangedIntegrationEvent>
{
    public TrustLevelChangedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        TrustLevelChangedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["previousLevel"] = integrationEvent.PreviousLevel;
        values["currentLevel"] = integrationEvent.CurrentLevel;
        values["platformTrustLevel"] = integrationEvent.PlatformTrustLevel;
        values["reason"] = integrationEvent.Reason;
        values["changedAt"] = integrationEvent.ChangedAt;

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.ChangedAt,
            values,
            subjectType: "trust",
            subjectId: integrationEvent.UserId.ToString());
    }
}
