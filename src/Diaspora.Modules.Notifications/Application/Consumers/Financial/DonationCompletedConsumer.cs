using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A donation completed — receipted to the donor.
/// <para>
/// <b>An anonymous donation produces no notification, and B15 says why in its own catalogue: the
/// contract "carries no identifier to send it to".</b> The guard here is on the identifier rather
/// than on the flag, which is the stronger of the two tests — a donation that is anonymous always
/// lacks the identifier, and one that lacks the identifier cannot be receipted whether or not
/// anybody set the flag.
/// </para>
/// <para>
/// The beneficiary is not told. A donation is between the donor and the platform's ledger; telling a
/// cause's organizer that a named member gave would leak exactly the thing anonymity protects, and
/// telling them about non-anonymous donations only would leak it by omission.
/// </para>
/// </summary>
public sealed class DonationCompletedConsumer
    : ModuleEventConsumer<DonationCompletedIntegrationEvent>
{
    public DonationCompletedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        DonationCompletedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.IsAnonymous || integrationEvent.DonorUserId is not { } donorUserId)
        {
            yield break;
        }

        var values = Values();
        values["amount"] = FinancialConsumerConstants.Money(
            integrationEvent.AmountMinorUnits, integrationEvent.Currency);

        values["receiptReference"] = integrationEvent.ReceiptReference;
        values["beneficiaryType"] = integrationEvent.BeneficiaryType;
        values["causeCode"] = integrationEvent.CauseCode;
        values["donationId"] = integrationEvent.DonationId;

        yield return Trigger(
            UserId.From(donorUserId),
            "donor",
            integrationEvent.CompletedAt,
            values,
            subjectType: "donation",
            subjectId: integrationEvent.DonationId.ToString());
    }
}
