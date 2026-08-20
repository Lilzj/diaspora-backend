using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Automation;

/// <summary>
/// Turns an automation's request to be delivered into a notification, and answers what became of it.
/// <para>
/// <b>FRONTEND-PHASE18-GAP-024's "internal contract by which the automation platform asks B13 to
/// deliver".</b> It is a consumer rather than an endpoint, because the register's alternative — a
/// service-to-service <c>POST /notifications/dispatch</c> — would be a route that must never be
/// reachable from a client, and a route that must never be reachable is worse than a route that does
/// not exist. The published contract joins the automation run's transaction, so a run that says
/// "asked" and a message nobody received cannot come apart.
/// </para>
/// <para>
/// <b>It names no template, no channel, no priority and no category.</b> Like every other consumer in
/// this module it builds a trigger and stops; the automation platform's own rule contributor supplies
/// the rest. That is what keeps "an automation decides when, B13 decides how" structural.
/// </para>
/// <para>
/// <b>The receipt is the addition, and it is opt-in.</b> An automation's run record has to be able to
/// say what happened, and "nothing happened" has four meanings a member needs told apart: no rule
/// matched, they are opted out, quiet hours held it, or it was a duplicate. This module already
/// published <c>NotificationCreated</c> for the positive case and published nothing at all for the
/// others, so a run's action outcome would have hung on <c>pending</c> for ever whenever a member's
/// own preferences worked correctly — the most common outcome in a healthy system. Only triggers that
/// ask for a receipt get one, so no other module's outbox volume changes.
/// </para>
/// </summary>
public sealed class AutomationNotificationConsumer
    : ModuleEventConsumer<AutomationDeliveryRequestedIntegrationEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly IIntegrationEventPublisher _publisher;

    /// <summary>
    /// Takes the dispatcher twice, and the duplication is deliberate.
    /// <para>
    /// The base's <c>DispatchOneAsync</c> discards the result, which is right for every other consumer
    /// — none of them has anywhere to put it. This one does: the receipt it publishes <i>is</i> the
    /// result, so it holds its own reference and reads what the dispatcher answered. Widening the
    /// base's helper would change a signature fourteen consumers depend on to serve one.
    /// </para>
    /// </summary>
    public AutomationNotificationConsumer(
        INotificationDispatcher dispatcher, IIntegrationEventPublisher publisher)
        : base(dispatcher)
    {
        _dispatcher = dispatcher;
        _publisher = publisher;
    }

    protected override string SourceModule => "Automation";

    protected override IEnumerable<NotificationTrigger> Translate(
        AutomationDeliveryRequestedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["automationName"] = integrationEvent.AutomationName;
        values["automationId"] = integrationEvent.AutomationId.ToString("D");
        values["summary"] = integrationEvent.Summary;
        values["scopeName"] = integrationEvent.ScopeName ?? integrationEvent.AutomationName;
        values["searchName"] = integrationEvent.AutomationName;
        values["subjectTitle"] = integrationEvent.SubjectTitle ?? integrationEvent.Summary;
        values["subjectId"] = integrationEvent.SubjectId ?? string.Empty;

        yield return Trigger(
            UserId.From(integrationEvent.RecipientUserId),
            integrationEvent.Audience,
            integrationEvent.OccurredAt,
            values,

            // The run and the action, carried through as the subject so the receipt can name them and
            // the automation platform can settle exactly one action outcome.
            subjectType: "automation-run",
            subjectId: $"{integrationEvent.RunId:N}:{integrationEvent.ActionId:N}",
            subjectMoment: integrationEvent.SubjectMoment,
            count: integrationEvent.Count,

            // Grouped on the run, so one automation reaching one member three ways is one thing in
            // their inbox rather than three.
            groupKey: integrationEvent.RunId == Guid.Empty
                ? null
                : integrationEvent.RunId.ToString("N"));
    }

    protected override async Task HandleAsync(
        AutomationDeliveryRequestedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var accepted = 0;
        var deduplicated = false;
        var noRuleMatched = false;

        foreach (var trigger in Translate(integrationEvent))
        {
            var result = await _dispatcher.DispatchAsync(trigger, cancellationToken);

            if (result.IsFailure)
            {
                // Counted as nothing accepted rather than retried here: the outbox redelivers on a
                // thrown exception, and the automation's own idempotency means a second attempt
                // cannot double-send. A receipt saying nothing arrived is the honest answer.
                continue;
            }

            accepted += result.Value.CreatedNotifications.Count;
            deduplicated |= result.Value.WasDeduplicated;
            noRuleMatched |= result.Value.NoRuleMatched;
        }

        if (!integrationEvent.RequiresSettlementReceipt || integrationEvent.RunId == Guid.Empty)
        {
            // A reminder and a saved-search alert have no run to settle. Publishing a receipt nothing
            // would read is outbox traffic with no reader.
            return;
        }

        await _publisher.PublishAsync(
            new AutomationDeliverySettledIntegrationEvent
            {
                AutomationId = integrationEvent.AutomationId,
                RunId = integrationEvent.RunId,
                ActionId = integrationEvent.ActionId,
                RecipientUserId = integrationEvent.RecipientUserId,
                Accepted = accepted,
                WasDeduplicated = deduplicated,
                NoRuleMatched = noRuleMatched,

                // Written for a member, not for an operator. A run record is a member-facing surface
                // and §36 forbids internal infrastructure detail on it.
                SuppressionReason = accepted > 0
                    ? null
                    : deduplicated
                        ? "You had already been told about this."
                        : noRuleMatched
                            ? "The platform had nothing to send for this."
                            : "Your notification settings meant nothing was sent.",
                OccurredAt = integrationEvent.OccurredAt
            },
            cancellationToken);
    }
}
