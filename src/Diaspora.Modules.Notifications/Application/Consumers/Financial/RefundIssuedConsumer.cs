using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A refund was issued against a payment.
/// <para>
/// A receipt, addressed to whoever paid. B15 issues refunds from staff tooling and from subject
/// retirement alike, so the member on the receiving end may have had no part in causing it — which is
/// exactly why they are told.
/// </para>
/// </summary>
public sealed class RefundIssuedConsumer : ModuleEventConsumer<RefundIssuedIntegrationEvent>
{
    public RefundIssuedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        RefundIssuedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amount"] = FinancialConsumerConstants.Money(
            integrationEvent.AmountMinorUnits, integrationEvent.Currency);

        values["reason"] = integrationEvent.Reason;
        values["isFullRefund"] = integrationEvent.IsFullRefund;
        values["paymentIntentId"] = integrationEvent.PaymentIntentId;
        values["refundId"] = integrationEvent.RefundId;

        yield return Trigger(
            UserId.From(integrationEvent.PayerUserId),
            "payer",
            integrationEvent.IssuedAt,
            values,
            subjectType: "payment",
            subjectId: integrationEvent.PaymentIntentId.ToString());
    }
}
