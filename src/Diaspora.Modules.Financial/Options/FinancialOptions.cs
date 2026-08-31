namespace Diaspora.Modules.Financial.Options;

/// <summary>
/// What a deployment can change about the Financial Platform without a release.
/// <para>
/// Every sweep has its own switch, for the reason the integration harness records: a background loop
/// querying a shared connection while a request is mid-command is a concurrent use SQLite will not
/// allow, and a test asserting on a side effect after a timer happens to have fired is a flaky test
/// wearing a passing badge. Every sweep also exposes a public <c>RunOnceAsync</c> so the suite drives
/// it deterministically.
/// </para>
/// </summary>
public sealed class FinancialOptions
{
    public const string SectionName = "Financial";

    /// <summary>The currency a financial identity is opened in when nothing else says.</summary>
    public string DefaultCurrency { get; set; } = "GBP";

    /// <summary>How long a payment intent stays open before the sweep expires it.</summary>
    public int PaymentIntentLifetimeHours { get; set; } = 168;

    /// <summary>How long a failed subscription renewal keeps access.</summary>
    public int SubscriptionGracePeriodDays { get; set; } = 14;

    /// <summary>How long the policy and catalogue read paths are cached.</summary>
    public int CatalogCacheSeconds { get; set; } = 300;

    /// <summary>How many payments one member may attempt in an hour before the assessor reviews.</summary>
    public int FraudVelocityAttemptsPerHour { get; set; } = 10;

    /// <summary>
    /// The most an unverified member may pay in a day, in minor units. Above it the shipped assessor
    /// asks for a review rather than refusing — see <c>VelocityFraudAssessor</c>.
    /// </summary>
    public long FraudUnverifiedDailyCeilingMinorUnits { get; set; } = 50_000;

    /// <summary>The gateway an adapter-less deployment uses. The mock, in development.</summary>
    public string DefaultGateway { get; set; } = "mock";

    /// <summary>Preferred adapters in order, when several can take a payment.</summary>
    public IList<string> GatewayPreference { get; set; } = [];

    public bool EnablePaymentExpirySweep { get; set; } = true;

    public bool EnablePlacementSweep { get; set; } = true;

    public bool EnableSubscriptionSweep { get; set; } = true;

    public bool EnableCreditExpirySweep { get; set; } = true;

    public bool EnableRevenueRollupSweep { get; set; } = true;

    public bool EnableGivingReconciliationSweep { get; set; } = true;

    public bool EnableCaching { get; set; } = true;

    public int PaymentExpirySweepMinutes { get; set; } = 15;

    public int PlacementSweepMinutes { get; set; } = 10;

    public int SubscriptionSweepMinutes { get; set; } = 60;

    public int CreditExpirySweepMinutes { get; set; } = 360;

    public int RevenueRollupSweepMinutes { get; set; } = 720;

    public int GivingReconciliationSweepMinutes { get; set; } = 1440;

    /// <summary>How many rows a sweep takes in one pass.</summary>
    public int SweepBatchSize { get; set; } = 200;

    // ── B15.1 ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether members may add money to their own balance.
    /// <para>
    /// Off by default, and deliberately so. A deposit is the platform holding somebody else's money,
    /// which is a regulatory position rather than a feature — a deployment turns it on when it has
    /// decided it may. The routes answer <c>financial.deposit.not_supported</c> until it does, which
    /// is a great deal more useful than a 404 on a route the client expects to exist.
    /// </para>
    /// </summary>
    public bool EnableDeposits { get; set; }

    /// <summary>
    /// Whether members may ask for a refund.
    /// <para>
    /// On by default: a request costs nothing and moves nothing, and a platform where the only way to
    /// ask is a support email is a platform where most people do not ask.
    /// </para>
    /// </summary>
    public bool EnableRefundRequests { get; set; } = true;

    /// <summary>
    /// Whether reward points may be converted into credit.
    /// <para>
    /// Off by default. Conversion turns something the platform invented into something it owes, and
    /// no deployment should acquire that by upgrading.
    /// </para>
    /// </summary>
    public bool EnableRewardConversion { get; set; }

    /// <summary>How many days a member has to ask for a refund after a payment captured.</summary>
    public int RefundRequestWindowDays { get; set; } = 30;

    /// <summary>How much warning a member gets before a subscription or placement lapses.</summary>
    public int ExpiryNoticeLeadDays { get; set; } = 3;

    public bool EnableExpiryNoticeSweep { get; set; } = true;

    public bool EnableWithdrawalSweep { get; set; } = true;

    /// <summary>
    /// How long an instruction whose outcome is unknown is left alone before a status provider is
    /// asked about it.
    /// <para>
    /// Not zero: a provider asked about a transfer it is still creating answers "no such thing",
    /// and acting on that would fail a payout that was about to succeed.
    /// </para>
    /// </summary>
    public int WithdrawalResolutionGraceMinutes { get; set; } = 15;

    /// <summary>
    /// Whether the platform asks providers about payments it believes are still in flight.
    /// <para>
    /// Requires a registered <c>IPaymentReconciliationProvider</c>; the sweep does nothing without
    /// one rather than pretending it reconciled.
    /// </para>
    /// </summary>
    public bool EnableReconciliationSweep { get; set; } = true;

    public int ExpiryNoticeSweepMinutes { get; set; } = 60;

    public int WithdrawalSweepMinutes { get; set; } = 5;

    public int ReconciliationSweepMinutes { get; set; } = 60;

    /// <summary>
    /// Whether the platform checks its payouts against the provider's own record.
    /// <para>
    /// The only check that can find a transfer the provider made with no withdrawal behind it, which
    /// is money gone with nothing accounting for it. Does nothing without a registered
    /// <c>IPayoutReconciliationProvider</c>, and none ships.
    /// </para>
    /// </summary>
    public bool EnablePayoutReconciliationSweep { get; set; } = true;

    public int PayoutReconciliationSweepMinutes { get; set; } = 360;

    /// <summary>
    /// How far back each pass looks. Wide enough to cover a provider that settles slowly and a
    /// deployment that was down for a while, because a window that misses a payout misses it
    /// permanently — nothing else looks for one.
    /// </summary>
    public int PayoutReconciliationWindowHours { get; set; } = 72;

    /// <summary>
    /// How long a captured payment may go unsettled before the reconciliation sweep asks about it.
    /// <para>
    /// Long enough that an ordinary settlement delay does not produce a query on every pass, short
    /// enough that a payment stuck between the platform's belief and the provider's record is found
    /// the same day.
    /// </para>
    /// </summary>
    public int ReconciliationAfterHours { get; set; } = 24;

    // ── B15.1 Giving Extension ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether a member-created campaign needs a staff decision before it can collect.
    /// <para>
    /// <b>On by default, and that default is the cautious one.</b> A campaign is a request for
    /// strangers' money in somebody's name; a deployment that turns this off publishes on submission
    /// and takes the fraud risk knowingly rather than by omission.
    /// </para>
    /// </summary>
    public bool RequireCampaignReview { get; set; } = true;

    /// <summary>
    /// Whether the platform charges recurring donations at all.
    /// <para>
    /// Off would leave <c>recurringIntervalMonths</c> exactly where B15 left it — recorded and not
    /// scheduled. On is what closes COMMUNITY-LIFE-GAP-008, and a deployment with no gateway able to
    /// take a repeat payment should leave it off rather than accumulate failures.
    /// </para>
    /// </summary>
    public bool EnableRecurringDonations { get; set; } = true;

    /// <summary>Whether the worker that charges recurring donations runs on this instance.</summary>
    public bool EnableRecurringDonationSweep { get; set; } = true;

    public int RecurringDonationSweepMinutes { get; set; } = 30;

    /// <summary>
    /// How many days after a failed charge the schedule tries again.
    /// <para>
    /// Three, because the two commonest causes — a card with no funds on it and a card that has just
    /// expired — are both often fixed within a few days, and retrying tomorrow hits the same wall.
    /// </para>
    /// </summary>
    public int RecurringDonationRetryDays { get; set; } = 3;

    /// <summary>Whether the campaign deadline and milestone sweep runs on this instance.</summary>
    public bool EnableCampaignSweep { get; set; } = true;

    public int CampaignSweepMinutes { get; set; } = 30;

    /// <summary>
    /// Whether the sweep re-announces membership bindings whose activation may not have landed.
    /// <para>
    /// This is the recoverability half of GAP-003: a capture that succeeded while the activation that
    /// should have followed did not. Turning it off leaves that failure permanent.
    /// </para>
    /// </summary>
    public bool EnableMembershipBindingReconciliation { get; set; } = true;

    /// <summary>
    /// How long after binding before an unconfirmed activation is re-announced.
    /// <para>
    /// Long enough that ordinary outbox latency is not mistaken for a failure. Bounded by
    /// <see cref="MembershipBindingMaxReannouncements"/> so a membership nothing can activate is not
    /// re-announced for ever.
    /// </para>
    /// </summary>
    public int MembershipBindingReconciliationAfterMinutes { get; set; } = 30;

    public int MembershipBindingMaxReannouncements { get; set; } = 5;

    /// <summary>
    /// The widest window a donation time series will answer for, in days.
    /// <para>
    /// A cap rather than a rejection: a wider request is clamped and the response says what window it
    /// actually covers, because a browse screen asking for "all time" should get a chart rather than
    /// a 400.
    /// </para>
    /// </summary>
    public int MaxTimeSeriesDays { get; set; } = 400;
}
