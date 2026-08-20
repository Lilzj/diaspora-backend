using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Financial;

/// <summary>
/// A wallet was frozen, closed or reactivated.
/// <para>
/// <b>Only a personally owned wallet produces a message, and the guard is the reason PSR-1.1 enriched
/// the contract.</b> B15's wallets belong to members, community organizations, providers, businesses
/// and the platform itself. Four of those five owner identifiers are not user identifiers, so a
/// consumer that addressed <c>OwnerId</c> directly would post "your wallet was frozen" to an
/// organization id — which resolves to nobody and fails in silence. The platform's own revenue wallet
/// would have been the loudest case: it is frozen and thawed by operations routinely.
/// </para>
/// <para>
/// <b>An event published before the enrichment carries no owner type, and produces nothing.</b> That
/// is the safe direction: a receipt nobody receives is recoverable, a private financial fact
/// delivered to the wrong account is not.
/// </para>
/// </summary>
public sealed class WalletStatusChangedConsumer
    : ModuleEventConsumer<WalletStatusChangedIntegrationEvent>
{
    /// <summary>
    /// The one owner type that is a person. Matched case-insensitively against the enum name B15
    /// stringifies, rather than parsed into an enum this module has no business knowing.
    /// </summary>
    private const string PersonalOwnerType = "Personal";

    public WalletStatusChangedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        WalletStatusChangedIntegrationEvent integrationEvent)
    {
        if (!string.Equals(integrationEvent.OwnerType, PersonalOwnerType, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var values = Values();
        values["status"] = integrationEvent.Status;
        values["previousStatus"] = integrationEvent.PreviousStatus;
        values["currency"] = integrationEvent.Currency;

        values["reason"] = FinancialConsumerConstants.FirstNonEmpty(
            integrationEvent.Reason, "no reason was recorded");

        values["walletId"] = integrationEvent.WalletId;

        yield return Trigger(
            UserId.From(integrationEvent.OwnerId),
            "wallet-owner",
            integrationEvent.ChangedAt,
            values,
            subjectType: "wallet",
            subjectId: integrationEvent.WalletId.ToString(),

            // Grouped on the wallet so the ten-minute deduplication window in the rule collapses a
            // retry storm rather than a genuine sequence of different transitions.
            groupKey: integrationEvent.WalletId.ToString());
    }
}
