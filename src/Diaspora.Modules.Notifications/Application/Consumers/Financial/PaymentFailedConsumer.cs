using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A payment attempt failed.
/// <para>
/// The one financial message where the member has to act, and where not acting has a consequence —
/// which is why its rule is the only <c>Critical</c> one in the billing category.
/// </para>
/// </summary>
public sealed class PaymentFailedConsumer : ModuleEventConsumer<PaymentFailedIntegrationEvent>
{
    public PaymentFailedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        PaymentFailedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["amount"] = FinancialConsumerConstants.Money(
            integrationEvent.AmountMinorUnits, integrationEvent.Currency);

        // The provider's message when there is one, its code when there is not, and a plain
        // fallback when the provider gave neither. A body that rendered "Reason: " with nothing
        // after it reads as a broken notification rather than an unexplained decline.
        values["reason"] = FinancialConsumerConstants.FirstNonEmpty(
            integrationEvent.FailureMessage,
            integrationEvent.FailureCode,
            "the payment was declined");

        values["isRetryable"] = integrationEvent.IsRetryable;
        values["paymentIntentId"] = integrationEvent.PaymentIntentId;

        yield return Trigger(
            UserId.From(integrationEvent.PayerUserId),
            "payer",
            integrationEvent.FailedAt,
            values,
            subjectType: "payment",
            subjectId: integrationEvent.PaymentIntentId.ToString());
    }
}
