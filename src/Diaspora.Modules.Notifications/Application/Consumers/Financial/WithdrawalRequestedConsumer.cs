using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// Somebody asked to take money out of a member's wallet (BSEC-007).
/// <para>
/// <b>The request, and only the request.</b> This yields nothing for any other transition, which is
/// deliberate on two counts. It keeps the notice to the one moment a member can still intervene —
/// a requested withdrawal is cancellable, a processing one is not. And it makes the message safe to
/// deduplicate: the subject is the withdrawal, and a withdrawal is requested exactly once, so the
/// platform's default collapse window can never swallow a second fact about it.
/// </para>
/// <para>
/// <b>Requested is recognised by having no previous status</b>, which is what
/// <c>WithdrawalStatusChangedIntegrationEvent</c> now carries for the request. Matching on the
/// status string would also match a reversal that happened to land back there, and nothing in the
/// contract promises it will not.
/// </para>
/// <para>
/// Security rather than billing, and Critical, for the reason the sibling consumer gives: a member
/// who has muted their money messages must still hear that their money is leaving.
/// </para>
/// </summary>
public sealed class WithdrawalRequestedConsumer
    : ModuleEventConsumer<WithdrawalStatusChangedIntegrationEvent>
{
    public WithdrawalRequestedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        WithdrawalStatusChangedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.PreviousStatus is not null)
        {
            yield break;
        }

        var values = Values();

        // The gross, which is what left the available balance. The fee is a separate fact and
        // putting two numbers in a security notice invites the member to work out which is which
        // instead of noticing whether they made the request.
        values["amount"] = FinancialConsumerConstants.Money(
            integrationEvent.AmountMinorUnits, integrationEvent.Currency);

        values["destination"] = integrationEvent.DestinationLabel ?? "your payout account";

        yield return Trigger(
            UserId.From(integrationEvent.RequestedByUserId),
            "requester",
            integrationEvent.ChangedAt,
            values,
            subjectType: "withdrawal",
            subjectId: integrationEvent.WithdrawalId.ToString());
    }
}
