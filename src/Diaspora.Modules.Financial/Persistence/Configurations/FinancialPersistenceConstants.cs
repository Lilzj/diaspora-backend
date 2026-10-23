namespace Diaspora.Modules.Financial.Persistence.Configurations;

/// <summary>
/// Table names and column widths, in one place.
/// <para>
/// Declared once rather than inline for the reason every module since B5 records: a name repeated in
/// two configurations is a name that will differ in one of them, and the difference shows up as a
/// migration that drops a table.
/// </para>
/// <para>
/// <b>Every table is prefixed <c>financial_</c>.</b> SQLite ignores schemas, and SQLite is the
/// provider the whole integration suite runs on — so two modules mapping one bare table name share a
/// table, and the failure names a test rather than the modules that collided. PSR-1 made that a
/// build failure; this module was written after it and simply avoids the collision.
/// </para>
/// </summary>
internal static class FinancialPersistenceConstants
{
    public const string AccountsTable = "financial_accounts";
    public const string WalletsTable = "financial_wallets";
    public const string LedgerEntriesTable = "financial_ledger_entries";
    public const string TransactionsTable = "financial_transactions";
    public const string PaymentIntentsTable = "financial_payment_intents";
    public const string PaymentAttemptsTable = "financial_payment_attempts";
    public const string RefundsTable = "financial_payment_refunds";
    public const string PoliciesTable = "financial_policies";
    public const string PolicyParametersTable = "financial_policy_parameters";
    public const string ProductsTable = "financial_products";
    public const string ProductEligibilityTable = "financial_product_eligibility";
    public const string PurchasesTable = "financial_purchases";
    public const string PlacementsTable = "financial_placements";
    public const string PlansTable = "financial_subscription_plans";
    public const string PlanEntitlementsTable = "financial_plan_entitlements";
    public const string SubscriptionsTable = "financial_subscriptions";
    public const string SubscriptionEntitlementsTable = "financial_subscription_entitlements";
    public const string PromotionsTable = "financial_promotions";
    public const string RedemptionsTable = "financial_promotion_redemptions";
    public const string CreditGrantsTable = "financial_credit_grants";
    public const string CausesTable = "financial_donation_causes";
    public const string DonationsTable = "financial_donations";
    public const string FundsTable = "financial_contribution_funds";
    public const string ContributionsTable = "financial_contributions";
    public const string RevenueTable = "financial_revenue_records";
    public const string CurrenciesTable = "financial_currencies";
    public const string WebhookEventsTable = "financial_webhook_events";
    public const string DepositsTable = "financial_deposits";
    public const string PayoutDestinationsTable = "financial_payout_destinations";
    public const string WithdrawalsTable = "financial_withdrawals";
    public const string RefundRequestsTable = "financial_refund_requests";
    public const string InvoicesTable = "financial_invoices";
    public const string RewardAccountsTable = "financial_reward_accounts";
    public const string RewardEntriesTable = "financial_reward_entries";
    public const string RewardHoldDecisionsTable = "financial_reward_hold_decisions";
    public const string RewardConversionsTable = "financial_reward_conversions";

    // ── B15.1 Giving Extension ───────────────────────────────────────────────────────────────

    public const string MembershipPaymentBindingsTable = "financial_membership_payment_bindings";
    public const string CampaignsTable = "financial_campaigns";
    public const string CampaignMilestonesTable = "financial_campaign_milestones";
    public const string CampaignFundUsageTable = "financial_campaign_fund_usage";
    public const string CampaignUpdatesTable = "financial_campaign_updates";
    public const string RecurringDonationsTable = "financial_recurring_donations";

    // -- B16 Advertising ----------------------------------------------------------------------
    //
    // `financial_ad_campaigns`, not `financial_campaigns`: that name holds B15.1's fundraising
    // campaigns and has since they shipped. The entity that arrived second takes the longer name.

    public const string AdCampaignsTable = "financial_ad_campaigns";
    public const string AdCampaignPlacementsTable = "financial_ad_campaign_placements";
    public const string AdPerformanceTable = "financial_ad_performance_days";

    /// <summary>Enum names are stored as text; nothing in this module has a longer one.</summary>
    public const int EnumLength = 32;

    public const int CurrencyLength = 3;
    public const int CountryLength = 8;
    public const int CodeLength = 64;
    public const int NameLength = 160;
    public const int DescriptionLength = 1024;

    /// <summary>
    /// A campaign's story or an update's body. Somebody explaining why their father needs an
    /// operation is not going to fit in a description field, and truncating it silently is worse
    /// than refusing it.
    /// </summary>
    public const int StoryLength = 8192;
    public const int ReasonLength = 512;
    public const int ReferenceLength = 256;
    public const int LabelLength = 128;
    public const int PayloadLength = 2048;
    public const int MediaReferenceLength = 256;
    public const int JoinedListLength = 1024;

    /// <summary>
    /// A percentage or a weight. Eighteen digits with six decimals — enough for a commission
    /// expressed to a thousandth of a percent, which is more precision than any policy will use and
    /// less than a <c>double</c> would silently lose.
    /// </summary>
    public const int RatePrecision = 18;

    public const int RateScale = 6;

    /// <summary>
    /// A pledge quantity. Eighteen digits with two decimals: forty-seven coats, six and a half
    /// hours. Money is never stored at this precision — it is a 64-bit integer count of minor units.
    /// </summary>
    public const int QuantityPrecision = 18;

    public const int QuantityScale = 2;
}
