using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A purchased placement went live.
/// <para>
/// Addressed to whoever bought it, not to whoever owns the subject — B15 allows a placement to be
/// bought over somebody else's listing, and the person who paid is the one entitled to know it
/// started.
/// </para>
/// <para>
/// <b>The priority weight is deliberately not in the message.</b> It is the number B14's ranking
/// reads; a member told their boost is worth 1.4 would reasonably ask 1.4 of what, and there is no
/// honest answer that does not describe the ranking engine.
/// </para>
/// </summary>
public sealed class PlacementActivatedConsumer
    : ModuleEventConsumer<PlacementActivatedIntegrationEvent>
{
    public PlacementActivatedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        PlacementActivatedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["kind"] = integrationEvent.Kind;
        values["subjectType"] = integrationEvent.SubjectType;
        values["subjectId"] = integrationEvent.SubjectId;
        values["productCode"] = integrationEvent.ProductCode;
        values["endsAt"] = integrationEvent.EndsAt;
        values["placementId"] = integrationEvent.PlacementId;

        yield return Trigger(
            UserId.From(integrationEvent.PurchasedByUserId),
            "purchaser",
            integrationEvent.ActivatedAt,
            values,
            subjectType: "placement",
            subjectId: integrationEvent.PlacementId.ToString());
    }
}
