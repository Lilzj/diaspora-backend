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
/// <para>
/// <b>And the payee, since 7 Nov 2026:</b> the member the money was taken back from, who lost a
/// balance and was told nothing. Today that is only ever the payer of a deposit, whose own wallet the
/// deposit credited. Then one notice says both things (taken out of the wallet, returned to the card)
/// rather than two notices about one refund; the payer receipt goes only when payer and payee differ.
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

        var payee = integrationEvent.PayeeUserId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var payeeIsPayer = payee == integrationEvent.PayerUserId;

        if (!payeeIsPayer)
        {
            yield return Trigger(
                UserId.From(integrationEvent.PayerUserId),
                "payer",
                integrationEvent.IssuedAt,
                values,
                subjectType: "payment",
                subjectId: integrationEvent.PaymentIntentId.ToString());
        }

        if (payee is not { } payeeUserId)
        {
            yield break;
        }

        var payeeValues = Values();
        payeeValues["amount"] = values["amount"];
        payeeValues["reasonlabel"] = ReasonLabel(integrationEvent.Reason, payeeIsPayer);
        payeeValues["returnedto"] = payeeIsPayer
            ? "the card or account you paid it in from"
            : "the person who paid it";
        payeeValues["paymentIntentId"] = integrationEvent.PaymentIntentId;
        payeeValues["refundId"] = integrationEvent.RefundId;

        yield return Trigger(
            UserId.From(payeeUserId),
            "payee",
            integrationEvent.IssuedAt,
            payeeValues,

            // The refund, not the payment: two partial refunds of one deposit are two withdrawals
            // from the member's balance, and the second is not a repeat of the first.
            subjectType: "refund",
            subjectId: integrationEvent.RefundId.ToString());
    }

    /// <summary>
    /// The refund reason in words, for the member who lost the money (7 Nov 2026). The payer receipt
    /// still shows B15's own word, as it always has.
    /// </summary>
    internal static string ReasonLabel(string reason, bool payeeIsPayer) => reason switch
    {
        "RequestedByPayer" => payeeIsPayer ? "you asked for it back" : "the person who paid asked for it back",
        "Duplicate" => "the payment was a duplicate",
        "Fraudulent" => "the payment was found to be fraudulent",
        "NotDelivered" => "what was paid for was not delivered",
        "PolicyRule" => "a platform rule required it",
        "Goodwill" => "Diaspora refunded it as a goodwill gesture",
        _ => string.IsNullOrWhiteSpace(reason) ? "no reason was recorded" : reason.Trim()
    };
}
