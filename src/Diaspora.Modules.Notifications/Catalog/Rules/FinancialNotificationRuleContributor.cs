using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Routing;

namespace Diaspora.Modules.Notifications.Catalog.Rules;

/// <summary>
/// How the Financial Platform's published facts become messages.
/// <para>
/// <b>The <c>billing</c> category was seeded by B13 and nothing routed to it for two phases.</b> B15
/// shipped seventeen published contracts and states plainly in its own event catalogue that it
/// "creates no notification, ever" — correctly, because a financial module that wrote notifications
/// would own a second delivery path. What it also did was name the six contracts B13 "would most
/// obviously act on" and stop there. PSR-1.1 is the phase that reads that list and acts on it.
/// </para>
/// <para>
/// <b>Nothing here required a change to B15.</b> Every rule keys on a contract that already exists
/// and every payload already carries whoever it is about — which is the whole argument for consumers
/// depending on contracts rather than modules, demonstrated rather than asserted.
/// </para>
/// <para>
/// <b>Money messages do not deduplicate and do not batch.</b> Every other category on the platform
/// collapses a burst into one line, because six reactions in an evening are one conversation. Two
/// failed payments are two facts about a member's money and the second is not a repeat of the first.
/// The one exception is the wallet freeze, which a retry storm can raise repeatedly for one cause.
/// </para>
/// </summary>
public sealed class FinancialNotificationRuleContributor : INotificationRuleContributor
{
    private static readonly NotificationChannelKind[] Everywhere =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push, NotificationChannelKind.Email];

    private static readonly NotificationChannelKind[] InAppAndPush =
        [NotificationChannelKind.InApp, NotificationChannelKind.Push];

    /// <summary>
    /// A receipt is a record, not an interruption.
    /// <para>
    /// In-app and email, never push. A member who has just completed a donation is holding the phone
    /// that completed it; buzzing it to say so is the kind of notification people turn the category
    /// off over — and the category they would turn off is the one carrying the payment failures.
    /// </para>
    /// </summary>
    private static readonly NotificationChannelKind[] Receipt =
        [NotificationChannelKind.InApp, NotificationChannelKind.Email];

    public string Module => NotificationsModuleMetadata.Name;

    public IEnumerable<NotificationRuleSeed> GetRules()
    {
        // ── Payments ─────────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "billing.payment.failed",
            EventName = typeof(PaymentFailedIntegrationEvent).FullName!,
            Audience = "payer",
            TemplateKey = NotificationTemplateKeys.PaymentFailed,
            CategoryKey = NotificationCategoryKeys.Billing,

            // The highest urgency in this contributor, and the only one that earns it: a failed
            // payment is the one financial event where the member has to do something and where
            // doing nothing has a consequence — a lapsed subscription, an expired boost, a
            // reservation released to somebody else.
            Priority = NotificationPriority.Critical,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "billing.refund.issued",
            EventName = typeof(RefundIssuedIntegrationEvent).FullName!,
            Audience = "payer",
            TemplateKey = NotificationTemplateKeys.RefundIssued,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        // ── Subscriptions ────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "billing.subscription.activated",
            EventName = typeof(SubscriptionActivatedIntegrationEvent).FullName!,
            Audience = "subscriber",
            TemplateKey = NotificationTemplateKeys.SubscriptionActivated,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        yield return new NotificationRuleSeed
        {
            Key = "billing.subscription.ended",
            EventName = typeof(SubscriptionEndedIntegrationEvent).FullName!,
            Audience = "subscriber",
            TemplateKey = NotificationTemplateKeys.SubscriptionEnded,
            CategoryKey = NotificationCategoryKeys.Billing,

            // High rather than Critical. B15 publishes an end for a cancellation the member
            // requested as well as for one they did not, and the consumer cannot tell the two apart
            // from the payload — so the urgency is set for the one that is merely informative.
            Priority = NotificationPriority.High,
            Channels = Everywhere
        };

        // ── Placements ───────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "billing.placement.activated",
            EventName = typeof(PlacementActivatedIntegrationEvent).FullName!,
            Audience = "purchaser",
            TemplateKey = NotificationTemplateKeys.PlacementActivated,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };

        yield return new NotificationRuleSeed
        {
            Key = "billing.placement.ended",
            EventName = typeof(PlacementEndedIntegrationEvent).FullName!,
            Audience = "purchaser",
            TemplateKey = NotificationTemplateKeys.PlacementEnded,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            Channels = InAppAndPush
        };

        // ── Giving ───────────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "billing.donation.receipt",
            EventName = typeof(DonationCompletedIntegrationEvent).FullName!,
            Audience = "donor",
            TemplateKey = NotificationTemplateKeys.DonationReceipt,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            Channels = Receipt
        };

        yield return new NotificationRuleSeed
        {
            Key = "billing.contribution.receipt",
            EventName = typeof(CommunityContributionCompletedIntegrationEvent).FullName!,
            Audience = "contributor",
            TemplateKey = NotificationTemplateKeys.ContributionReceipt,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.Normal,
            Channels = Receipt
        };

        // ── Wallet ───────────────────────────────────────────────────────

        yield return new NotificationRuleSeed
        {
            Key = "billing.wallet.status",
            EventName = typeof(WalletStatusChangedIntegrationEvent).FullName!,
            Audience = "wallet-owner",
            TemplateKey = NotificationTemplateKeys.WalletStatusChanged,
            CategoryKey = NotificationCategoryKeys.Billing,
            Priority = NotificationPriority.High,
            Channels = Everywhere,

            // The one deduplicating rule here. A freeze applied by a sweep that then retries can
            // raise the same transition several times in a minute, and a member does not need to be
            // told three times that their wallet is frozen. The window is short enough that a
            // genuine freeze-then-unfreeze-then-freeze still produces three messages.
            DeduplicationWindow = TimeSpan.FromMinutes(10)
        };
    }
}
