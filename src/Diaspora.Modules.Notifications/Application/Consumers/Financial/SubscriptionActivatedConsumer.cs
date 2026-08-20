using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A subscription became active.
/// <para>
/// <b>A renewal produces nothing, and that is the whole design of this consumer.</b> B15 publishes
/// one contract for the first activation and for every monthly renewal thereafter, distinguished by
/// <c>IsRenewal</c>. Telling a member their plan is active on the same day every month for a year is
/// how the billing category — the category that also carries their payment failures — gets muted.
/// </para>
/// <para>
/// A trial activation is announced. It is the member's first sight of the tier, and a trial that
/// starts silently is one they will not use before it lapses.
/// </para>
/// </summary>
public sealed class SubscriptionActivatedConsumer
    : ModuleEventConsumer<SubscriptionActivatedIntegrationEvent>
{
    public SubscriptionActivatedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        SubscriptionActivatedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.IsRenewal)
        {
            yield break;
        }

        var values = Values();
        values["tier"] = integrationEvent.Tier;
        values["planCode"] = integrationEvent.PlanCode;
        values["interval"] = integrationEvent.Interval;
        values["isTrial"] = integrationEvent.IsTrial;
        values["periodEndsAt"] = integrationEvent.PeriodEndsAt;
        values["subscriptionId"] = integrationEvent.SubscriptionId;

        yield return Trigger(
            UserId.From(integrationEvent.SubscriberUserId),
            "subscriber",
            integrationEvent.ActivatedAt,
            values,
            subjectType: "subscription",
            subjectId: integrationEvent.SubscriptionId.ToString());
    }
}
