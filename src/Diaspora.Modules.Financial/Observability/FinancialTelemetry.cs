using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Financial.Observability;

/// <summary>
/// The Financial Platform's activity source and meters.
/// <para>
/// <b>No counter here records an amount.</b> Metrics are exported to whatever the deployment
/// aggregates them with, and a gauge carrying "money captured today" is a business figure on an
/// operations channel — a place with different retention, different access control and different
/// people looking at it. Counts, outcomes and durations only; the ledger is where amounts live.
/// </para>
/// </summary>
public static class FinancialTelemetry
{
    public const string SourceName = "Diaspora.Financial";

    public static readonly ActivitySource Source = new(SourceName);

    public static readonly Meter Meter = new(SourceName);

    /// <summary>Payments attempted, tagged by outcome. Never by amount.</summary>
    public static readonly Counter<long> PaymentsAttempted =
        Meter.CreateCounter<long>("diaspora.financial.payments_attempted");

    /// <summary>Ledger entries appended. The best single indicator that money is moving.</summary>
    public static readonly Counter<long> LedgerEntriesAppended =
        Meter.CreateCounter<long>("diaspora.financial.ledger_entries_appended");

    /// <summary>
    /// Postings refused because the legs did not balance.
    /// <para>
    /// <b>This one should always be zero, and an alert on it is worth having.</b> An unbalanced
    /// posting is a caller defect, and a non-zero value means somebody is trying to move money in a
    /// way the ledger cannot express.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UnbalancedPostings =
        Meter.CreateCounter<long>("diaspora.financial.unbalanced_postings");

    /// <summary>
    /// Authorisations whose capture the provider refused, tagged by provider.
    /// <para>
    /// <b>Every one of these is money held at a provider that this platform has not taken.</b> The
    /// authorisation stands, the capture did not happen, and nothing moves the payment again:
    /// expiry covers <c>Draft</c> and <c>Pending</c> only, and no sweep retries a capture. A
    /// non-zero value is a payment somebody has to finish by hand.
    /// </para>
    /// </summary>
    public static readonly Counter<long> CapturesRefused =
        Meter.CreateCounter<long>("diaspora.financial.captures_refused");

    /// <summary>Placements activated and ended, tagged by kind.</summary>
    public static readonly Counter<long> PlacementsChanged =
        Meter.CreateCounter<long>("diaspora.financial.placements_changed");

    /// <summary>
    /// Prices the policy engine could not resolve.
    /// <para>
    /// The other counter worth alerting on. An unresolved policy is a refusal rather than a silent
    /// zero — which is the correct behaviour and also means a member could not buy something.
    /// </para>
    /// </summary>
    public static readonly Counter<long> UnresolvedPolicies =
        Meter.CreateCounter<long>("diaspora.financial.unresolved_policies");

    /// <summary>
    /// Provider callbacks received, tagged by provider and outcome.
    /// <para>
    /// <b>The <c>unverified</c> tag is the third counter worth alerting on.</b> A signature that does
    /// not verify is either a misconfigured secret — every payment silently stops confirming — or
    /// somebody posting to an unauthenticated endpoint that moves money. Both need a person.
    /// <c>duplicate</c> is expected and healthy: it is the replay barrier working.
    /// </para>
    /// </summary>
    public static readonly Counter<long> WebhooksReceived =
        Meter.CreateCounter<long>("diaspora.financial.webhooks_received");

    /// <summary>Deposits and withdrawals, tagged by outcome. Never by amount.</summary>
    public static readonly Counter<long> WalletMovements =
        Meter.CreateCounter<long>("diaspora.financial.wallet_movements");

    /// <summary>Reward points converted into credit, tagged by outcome. Never by amount.</summary>
    public static readonly Counter<long> RewardConversions =
        Meter.CreateCounter<long>("diaspora.financial.reward_conversions");

    /// <summary>Subscription plan changes, tagged by direction and outcome.</summary>
    public static readonly Counter<long> SubscriptionChanges =
        Meter.CreateCounter<long>("diaspora.financial.subscription_changes");

    /// <summary>
    /// Places where the platform's record and a provider's disagree.
    /// <para>
    /// <b>The fourth counter worth alerting on, and the one that means a person has to look.</b>
    /// Every other refusal in this module is a rule doing its job; a mismatch is the platform and its
    /// provider believing different things about somebody's money, and no automatic resolution is
    /// safe.
    /// </para>
    /// </summary>
    public static readonly Counter<long> ReconciliationMismatches =
        Meter.CreateCounter<long>("diaspora.financial.reconciliation_mismatches");
}
