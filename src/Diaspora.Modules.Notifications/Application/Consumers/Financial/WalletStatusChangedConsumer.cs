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

    /// <summary>The status a wallet is opened in. Leaving it is the wallet opening, not a decision.</summary>
    private const string PendingStatus = "Pending";

    public WalletStatusChangedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => FinancialConsumerConstants.SourceModule;

    protected override IEnumerable<NotificationTrigger> Translate(
        WalletStatusChangedIntegrationEvent integrationEvent)
    {
        if (!string.Equals(integrationEvent.OwnerType, PersonalOwnerType, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        // A wallet opening is not news (7 Nov 2026). Every wallet is activated the moment it is
        // opened, so registering, depositing in a new currency or receiving a first credit each sent
        // "Your wallet is now active: no reason was recorded", several at once. This notice is for a
        // decision about the member's money: a freeze, a closure, or a freeze lifted.
        if (string.Equals(integrationEvent.PreviousStatus, PendingStatus, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var values = Values();
        values["status"] = integrationEvent.Status;
        values["previousStatus"] = integrationEvent.PreviousStatus;
        values["currency"] = integrationEvent.Currency;

        // Trailing full stops trimmed: the template ends the sentence, and staff often end theirs.
        values["reason"] = FinancialConsumerConstants.FirstNonEmpty(
            integrationEvent.Reason, "no reason was recorded").TrimEnd('.', ' ');

        values["statuslabel"] = integrationEvent.Status.ToLowerInvariant();
        values["nexttext"] = NextStep(integrationEvent.Status);

        values["walletId"] = integrationEvent.WalletId;

        yield return Trigger(
            UserId.From(integrationEvent.OwnerId),
            "wallet-owner",
            integrationEvent.ChangedAt,
            values,
            subjectType: "wallet",
            subjectId: integrationEvent.WalletId.ToString(),

            // Grouped on the wallet. The rule no longer deduplicates (7 Nov 2026): its key could not
            // tell a freeze from the unfreeze that followed it.
            groupKey: integrationEvent.WalletId.ToString());
    }

    /// <summary>
    /// What the member can do now (7 Nov 2026). Actions are rendered nowhere, so this is in the body:
    /// a freeze is a staff decision about their money, and they can appeal it.
    /// </summary>
    private static string NextStep(string status) => status switch
    {
        "Frozen" => "Money can still arrive, but none can leave until the freeze is lifted. "
            + "If you think this is wrong, you can appeal from Profile, Appeals.",
        // No appeal is offered: a closed wallet cannot be reopened, so an upheld appeal could change
        // nothing, and a notice must not promise what the platform cannot do.
        "Closed" => "No money can move in or out of it any more.",
        "Active" => "You can use it as normal again.",
        _ => string.Empty
    };
}
