using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A subscription ended — cancelled, lapsed or expired.
/// <para>
/// B15's catalogue calls this one "what they lost", and the wording deliberately does not sell the
/// plan back. The member who cancelled on purpose reads the same message as the member whose card
/// expired, and only one of them wants an offer.
/// </para>
/// </summary>
public sealed class SubscriptionEndedConsumer
    : ModuleEventConsumer<SubscriptionEndedIntegrationEvent>
{
    public SubscriptionEndedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        SubscriptionEndedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["tier"] = integrationEvent.Tier;
        values["planCode"] = integrationEvent.PlanCode;
        values["status"] = integrationEvent.Status;
        values["reason"] = integrationEvent.Reason;
        values["subscriptionId"] = integrationEvent.SubscriptionId;

        yield return Trigger(
            UserId.From(integrationEvent.SubscriberUserId),
            "subscriber",
            integrationEvent.EndedAt,
            values,
            subjectType: "subscription",
            subjectId: integrationEvent.SubscriptionId.ToString());
    }
}
