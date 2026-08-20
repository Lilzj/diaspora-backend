using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A purchased placement finished.
/// <para>
/// B15 is careful that "lapsed, cancelled and refunded are three different things" and carries the
/// status to say which. The message states that the placement stopped; the status travels in the
/// values so a client that wants to distinguish them can, without the template asserting a
/// distinction the member did not ask about.
/// </para>
/// </summary>
public sealed class PlacementEndedConsumer : ModuleEventConsumer<PlacementEndedIntegrationEvent>
{
    public PlacementEndedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        PlacementEndedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["kind"] = integrationEvent.Kind;
        values["status"] = integrationEvent.Status;
        values["subjectType"] = integrationEvent.SubjectType;
        values["subjectId"] = integrationEvent.SubjectId;
        values["placementId"] = integrationEvent.PlacementId;

        yield return Trigger(
            UserId.From(integrationEvent.PurchasedByUserId),
            "purchaser",
            integrationEvent.EndedAt,
            values,
            subjectType: "placement",
            subjectId: integrationEvent.PlacementId.ToString());
    }
}
