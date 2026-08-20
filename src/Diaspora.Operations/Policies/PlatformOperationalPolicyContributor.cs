using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Policies;

namespace Diaspora.Operations.Policies;

/// <summary>
/// The platform's default cache, health, recovery and feature-flag policies.
/// <para>
/// Split from <see cref="PlatformResiliencePolicyContributor"/> along the line an operator would
/// split them: one file is "how the platform behaves when something is failing", this one is "how
/// long the platform keeps things and what it promises about them".
/// </para>
/// </summary>
public sealed class PlatformOperationalPolicyContributor : IOperationalPolicyContributor
{
    public string Module => "Platform";

    public IEnumerable<OperationalPolicy> Policies =>
    [
        // ─── Cache ────────────────────────────────────────────────────────────────────────
        new CachePolicy(OperationalPolicyNames.Cache.Default)
        {
            Description = "Any cached read that does not name its own policy.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            LocalExpiration = TimeSpan.FromSeconds(30)
        },

        new CachePolicy(OperationalPolicyNames.Cache.ReferenceData)
        {
            // Countries, categories, currencies. Changed by a seeder or a deployment, read on
            // nearly every request. Long absolute expiry and a tag so a reference-data change can
            // drop the whole family at once instead of waiting the hour out.
            Description = "Countries, categories, currencies. Long-lived; invalidated by tag on change.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromHours(6),
            LocalExpiration = TimeSpan.FromMinutes(10),
            Tags = [CacheTags.ReferenceData],
            WarmOnStartup = true,
            StaleWhileRevalidate = TimeSpan.FromMinutes(5)
        },

        new CachePolicy(OperationalPolicyNames.Cache.PersonalizedFeed)
        {
            // Per member, so the local tier is nearly useless — a member's next request rarely
            // lands on the same replica. Distributed only, and short: a feed that does not change
            // when a member acts feels broken in a way a stale country list does not.
            Description = "A member's own feed. Distributed only: the local tier would rarely hit.",
            Tier = CacheTier.Distributed,
            AbsoluteExpiration = TimeSpan.FromMinutes(3),

            // Explicitly cleared, because the record's default is thirty seconds and a distributed-only
            // policy has no local tier to expire. The validator refuses the combination — which it did,
            // at host start, on the first integration run of this phase.
            LocalExpiration = null,
            Tags = [CacheTags.Recommendations, CacheTags.PersonalizedFeed]
        },

        new CachePolicy(OperationalPolicyNames.Cache.CountryFeed)
        {
            Description = "The shared per-country feed. Read by everyone in a country; safe to share.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(10),
            LocalExpiration = TimeSpan.FromMinutes(1),
            Tags = [CacheTags.Recommendations, CacheTags.CountryFeed],
            StaleWhileRevalidate = TimeSpan.FromMinutes(2)
        },

        new CachePolicy(OperationalPolicyNames.Cache.Trending)
        {
            // Trending is a stored window (DEBT-022), so the underlying value changes on a sweep
            // rather than on a write. Caching for a fraction of the sweep interval is free accuracy.
            Description = "Trending. Recomputed on a sweep, so a short cache costs no accuracy.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            LocalExpiration = TimeSpan.FromMinutes(1),
            Tags = [CacheTags.Recommendations, CacheTags.Trending],
            StaleWhileRevalidate = TimeSpan.FromMinutes(1)
        },

        new CachePolicy(OperationalPolicyNames.Cache.ProviderDirectory)
        {
            Description = "The provider directory. Published rarely, browsed constantly.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(15),
            LocalExpiration = TimeSpan.FromMinutes(2),
            Tags = [CacheTags.Providers]
        },

        new CachePolicy(OperationalPolicyNames.Cache.GuideCatalog)
        {
            Description = "Published guides and the knowledge directory.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(30),
            LocalExpiration = TimeSpan.FromMinutes(5),
            Tags = [CacheTags.Guides],
            StaleWhileRevalidate = TimeSpan.FromMinutes(5)
        },

        new CachePolicy(OperationalPolicyNames.Cache.CountryIntelligence)
        {
            // Frozen at publication by B8.2, which makes it the safest thing on the platform to
            // cache: the value cannot change without a new publication, and a publication
            // invalidates the tag.
            Description = "Country intelligence. Frozen at publication, so a long cache cannot go wrong.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromHours(2),
            LocalExpiration = TimeSpan.FromMinutes(15),
            Tags = [CacheTags.CountryIntelligence],
            WarmOnStartup = true,
            StaleWhileRevalidate = TimeSpan.FromMinutes(30)
        },

        new CachePolicy(OperationalPolicyNames.Cache.FinancialCatalog)
        {
            // The monetization product catalogue and the policy catalogue — both reference data.
            // Wallet balances, ledger entries and payment intents are never cached, anywhere, and
            // no policy here would make that safe. Said out loud because the name invites it.
            Description =
                "Monetization products and financial policy — catalogue only. Balances are never cached.",
            Tier = CacheTier.MultiLevel,
            AbsoluteExpiration = TimeSpan.FromMinutes(30),
            LocalExpiration = TimeSpan.FromMinutes(5),
            Tags = [CacheTags.Financial, CacheTags.ReferenceData]
        },

        new CachePolicy(OperationalPolicyNames.Cache.FeatureFlags)
        {
            // Short, local-first, and never stale-while-revalidate. A kill switch that takes five
            // minutes to propagate is not a kill switch.
            Description = "Flag evaluations. Short and never served stale — a slow kill switch is no switch.",
            Tier = CacheTier.Local,
            AbsoluteExpiration = TimeSpan.FromSeconds(30),
            LocalExpiration = TimeSpan.FromSeconds(30),
            Tags = [CacheTags.FeatureFlags]
        },

        // ─── Health ───────────────────────────────────────────────────────────────────────
        new HealthPolicy(OperationalPolicyNames.Health.Default)
        {
            Description = "Any check that does not name its own policy. Degrades rather than fails.",
            Critical = false,
            Timeout = TimeSpan.FromSeconds(3),
            CacheDuration = TimeSpan.FromSeconds(5)
        },

        new HealthPolicy(OperationalPolicyNames.Health.Storage)
        {
            // The one genuinely critical dependency. An instance that cannot reach the database
            // cannot serve a single request, so removing it from rotation is correct.
            Description = "The database. Critical: an instance that cannot reach it serves nothing.",
            Critical = true,
            Timeout = TimeSpan.FromSeconds(5),
            CacheDuration = TimeSpan.FromSeconds(5),
            FailureThreshold = 2,
            RecoveryThreshold = 1
        },

        new HealthPolicy(OperationalPolicyNames.Health.Messaging)
        {
            Description = "The outbox and transport. Critical: a backlog nothing drains grows without bound.",
            Critical = true,
            Timeout = TimeSpan.FromSeconds(5),
            CacheDuration = TimeSpan.FromSeconds(10),
            FailureThreshold = 2
        },

        new HealthPolicy(OperationalPolicyNames.Health.Cache)
        {
            // Explicitly not critical. The cache is an optimisation; an instance whose cache is
            // down is slower, not broken, and taking it out of rotation makes the outage worse by
            // concentrating the load on the replicas whose cache is also down.
            Description = "The cache. Never critical — a cache outage must not become a platform outage.",
            Critical = false,
            Timeout = TimeSpan.FromSeconds(2),
            CacheDuration = TimeSpan.FromSeconds(10)
        },

        new HealthPolicy(OperationalPolicyNames.Health.Search)
        {
            Description = "Search. Never critical: every search port falls back to a database engine.",
            Critical = false,
            Timeout = TimeSpan.FromSeconds(3),
            CacheDuration = TimeSpan.FromSeconds(15)
        },

        new HealthPolicy(OperationalPolicyNames.Health.ExternalProvider)
        {
            Description = "Third-party providers. Degrade: their outage is not this instance's fault.",
            Critical = false,
            Timeout = TimeSpan.FromSeconds(5),
            CacheDuration = TimeSpan.FromSeconds(30),
            FailureThreshold = 3,
            RecoveryThreshold = 2
        },

        new HealthPolicy(OperationalPolicyNames.Health.BackgroundWorker)
        {
            Description = "Background workers. Degrade: a stalled sweep is not a reason to drop traffic.",
            Critical = false,
            Timeout = TimeSpan.FromSeconds(2),
            CacheDuration = TimeSpan.FromSeconds(10)
        },

        // ─── Recovery ─────────────────────────────────────────────────────────────────────
        new RecoveryPolicy(OperationalPolicyNames.Recovery.Default)
        {
            Description = "The platform's baseline objectives.",
            RecoveryPointObjective = TimeSpan.FromMinutes(15),
            RecoveryTimeObjective = TimeSpan.FromHours(4),
            BackupRetention = TimeSpan.FromDays(30),
            RestoreTestInterval = TimeSpan.FromDays(30)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.FinancialLedger)
        {
            // The strictest objectives on the platform. The ledger is append-only and every other
            // financial number is derived from it, so a lost minute of ledger is a lost minute of
            // balances, revenue and audit — none of which can be reconstructed from anywhere else.
            Description = "The ledger. Every financial number is derived from it; loss is not recoverable.",
            RecoveryPointObjective = TimeSpan.FromMinutes(1),
            RecoveryTimeObjective = TimeSpan.FromHours(1),
            RetentionPeriod = TimeSpan.FromDays(365 * 7),
            BackupRetention = TimeSpan.FromDays(365),
            ArchiveOnExpiry = true,
            RestoreTestInterval = TimeSpan.FromDays(7)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.Identity)
        {
            Description = "Accounts, sessions and security events.",
            RecoveryPointObjective = TimeSpan.FromMinutes(5),
            RecoveryTimeObjective = TimeSpan.FromHours(2),
            BackupRetention = TimeSpan.FromDays(90),
            ArchiveOnExpiry = true,
            RestoreTestInterval = TimeSpan.FromDays(30)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.Media)
        {
            // Media objects live in object storage with its own durability guarantee; what the
            // database holds is the reference. The RPO here is about the reference, and the
            // retention is what the privacy retention audit reads.
            Description = "Media references. The objects themselves live in storage with its own durability.",
            RecoveryPointObjective = TimeSpan.FromMinutes(15),
            RecoveryTimeObjective = TimeSpan.FromHours(6),
            RetentionPeriod = TimeSpan.FromDays(365 * 2),
            BackupRetention = TimeSpan.FromDays(30),
            ArchiveOnExpiry = true,
            RestoreTestInterval = TimeSpan.FromDays(90)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.Notifications)
        {
            Description = "Notification history. Valuable for a while, then noise.",
            RecoveryPointObjective = TimeSpan.FromHours(1),
            RecoveryTimeObjective = TimeSpan.FromHours(8),
            RetentionPeriod = TimeSpan.FromDays(180),
            BackupRetention = TimeSpan.FromDays(30),
            RestoreTestInterval = TimeSpan.FromDays(90)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.Recommendations)
        {
            // Derived data. It can be recomputed from the signals, so the objective is about how
            // long recomputation takes, not about how much is lost.
            Description = "Derived. Recomputable from signals, so the objective bounds recomputation.",
            RecoveryPointObjective = TimeSpan.FromHours(6),
            RecoveryTimeObjective = TimeSpan.FromHours(12),
            RetentionPeriod = TimeSpan.FromDays(90),
            BackupRetention = TimeSpan.FromDays(7),
            RestoreTestInterval = TimeSpan.FromDays(180)
        },

        new RecoveryPolicy(OperationalPolicyNames.Recovery.Telemetry)
        {
            Description = "Traces, metrics and logs. Owned by the aggregator; never restored here.",
            RecoveryPointObjective = TimeSpan.FromHours(24),
            RecoveryTimeObjective = TimeSpan.FromHours(24),
            RetentionPeriod = TimeSpan.FromDays(30),
            BackupRetention = TimeSpan.FromDays(7),
            RestoreTestInterval = TimeSpan.FromDays(365)
        },

        // ─── Feature flags ────────────────────────────────────────────────────────────────
        new FeatureFlagPolicy(OperationalPolicyNames.FeatureFlag.Default)
        {
            Description = "Every flag that does not name its own policy.",
            FailClosed = true,
            EvaluationCacheDuration = TimeSpan.FromSeconds(30),
            KillSwitchApplies = true,
            StaleAfter = TimeSpan.FromDays(90)
        },

        new FeatureFlagPolicy(OperationalPolicyNames.FeatureFlag.SafetyControl)
        {
            // The kill switch does not reach these, and they are not cached. A flag that governs
            // moderation, audit or rate limiting must take effect on the next evaluation, and must
            // not be disableable by the same lever that disables a feature.
            Description = "Flags guarding moderation, audit and limits. Outside the kill switch; never cached.",
            FailClosed = true,
            EvaluationCacheDuration = TimeSpan.Zero,
            KillSwitchApplies = false,
            StaleAfter = TimeSpan.FromDays(365)
        }
    ];
}
