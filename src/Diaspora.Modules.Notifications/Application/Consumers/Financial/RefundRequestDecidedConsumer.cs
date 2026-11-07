using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// Staff answered a member's refund request (7 Nov 2026).
/// <para>
/// <b>The event was published from the start and nothing consumed it,</b> so a member who asked for
/// their money back heard nothing either way: an approval surfaced only as the refund receipt, and a
/// refusal not at all. The member now reads the answer and the staff member's note, and, when the
/// answer is no, that they can appeal.
/// </para>
/// <para>
/// Only an approval or a refusal is an answer. A withdrawal is the member's own act and is not
/// published as a decision; anything else is ignored rather than announced in words that may be wrong.
/// </para>
/// </summary>
public sealed class RefundRequestDecidedConsumer : ModuleEventConsumer<RefundRequestDecidedIntegrationEvent>
{
    public RefundRequestDecidedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        RefundRequestDecidedIntegrationEvent integrationEvent)
    {
        var approved = string.Equals(integrationEvent.Decision, "Approved", StringComparison.OrdinalIgnoreCase);
        var rejected = string.Equals(integrationEvent.Decision, "Rejected", StringComparison.OrdinalIgnoreCase);

        if (!approved && !rejected)
        {
            yield break;
        }

        var values = Values();
        values["amount"] = FinancialConsumerConstants.Money(
            integrationEvent.AmountMinorUnits, integrationEvent.Currency);

        values["outcome"] = approved ? "approved" : "turned down";

        values["notetext"] = string.IsNullOrWhiteSpace(integrationEvent.DecisionNote)
            ? string.Empty
            : $"Diaspora's note: {integrationEvent.DecisionNote.Trim().TrimEnd('.')}. ";

        values["nexttext"] = approved
            ? "The refund is on its way back to you."
            : "If you think this is wrong, you can appeal from Profile, Appeals.";

        values["paymentIntentId"] = integrationEvent.PaymentIntentId;
        values["refundRequestId"] = integrationEvent.RefundRequestId;
        values["decision"] = integrationEvent.Decision;

        yield return Trigger(
            UserId.From(integrationEvent.RequestedByUserId),
            "requester",
            integrationEvent.DecidedAt,
            values,
            subjectType: "refund-request",
            subjectId: integrationEvent.RefundRequestId.ToString());
    }
}
