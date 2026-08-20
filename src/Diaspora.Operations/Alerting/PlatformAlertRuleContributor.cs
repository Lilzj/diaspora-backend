using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Telemetry;

namespace Diaspora.Operations.Alerting;

/// <summary>
/// The alert rules the platform ships.
/// <para>
/// One rule per condition the phase brief names, each with a threshold that is a starting point and a
/// runbook line that is not. Thresholds are expected to be retuned once the Part 13 baselines have run
/// against real traffic; the runbooks are expected to survive.
/// </para>
/// </summary>
public sealed class PlatformAlertRuleContributor : IAlertRuleContributor
{
    public string Module => "Platform";

    public IEnumerable<AlertRule> Rules =>
    [
        new AlertRule(
            "platform.error-rate.high",
            PlatformTelemetryNames.Requests.Failed,
            AlertComparison.GreaterThan,
            Threshold: 0.05,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Page,
            "Platform",
            "More than 5% of requests are failing.",
            "Check the platform-health board first: a critical health check explains most of these. " +
            "If health is green, look at the failing module in the performance board — the metric is " +
            "labelled by module — then at recent deployments and recent flag changes."),

        new AlertRule(
            "platform.latency.p99",
            PlatformTelemetryNames.Requests.Duration,
            AlertComparison.GreaterThan,
            Threshold: 2_000,
            For: TimeSpan.FromMinutes(10),
            AlertSeverity.Critical,
            "Platform",
            "The 99th percentile request is over two seconds.",
            "Check database command duration and cache hit ratio together. A latency rise with a " +
            "falling hit ratio is a cache problem; with a stable hit ratio it is a query or a lock."),

        new AlertRule(
            "messaging.outbox.backlog",
            PlatformTelemetryNames.Messaging.QueueDepth,
            AlertComparison.GreaterThan,
            Threshold: 5_000,
            For: TimeSpan.FromMinutes(10),
            AlertSeverity.Critical,
            "Platform",
            "The outbox is growing faster than it drains.",
            "Look at the oldest-message age beside the depth. Depth alone is not the signal: a large " +
            "queue that is draining is healthy, and a small one that has not moved in an hour is a " +
            "stalled dispatcher. Check the workers board for the dispatcher's last successful run.",
            new Dictionary<string, string> { [PlatformTelemetryNames.Attributes.Queue] = "outbox" }),

        new AlertRule(
            "messaging.outbox.stalled",
            PlatformTelemetryNames.Messaging.OldestAgeSeconds,
            AlertComparison.GreaterThan,
            Threshold: 900,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Page,
            "Platform",
            "The oldest undelivered message is over fifteen minutes old.",
            "The dispatcher is not draining. Check that at least one instance is running it, then the " +
            "transport's health, then the dead-letter count — a poison message at the head of a " +
            "partition blocks everything behind it."),

        new AlertRule(
            "messaging.dead-letters",
            PlatformTelemetryNames.Messaging.DeadLetters,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(15),
            AlertSeverity.Warning,
            "Platform",
            "Messages have been given up on.",
            "Inspect them through the outbox administration endpoint before replaying. A dead letter " +
            "is a fact that was never published; something downstream has a gap in its state."),

        new AlertRule(
            "cache.failure",
            PlatformTelemetryNames.Cache.Errors,
            AlertComparison.GreaterThan,
            Threshold: 10,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Warning,
            "Platform",
            "The cache store is failing.",
            "The platform keeps serving — every cache failure falls through to the source — so this is " +
            "a latency and load problem rather than an outage. Watch database command duration: the " +
            "load the cache was absorbing is now on the database."),

        new AlertRule(
            "cache.hit-ratio.low",
            PlatformTelemetryNames.Cache.Operations,
            AlertComparison.LessThan,
            Threshold: 0.5,
            For: TimeSpan.FromMinutes(30),
            AlertSeverity.Info,
            "Platform",
            "Fewer than half of cached reads are hitting.",
            "Usually a policy whose expiry is shorter than the read interval, or an invalidation firing " +
            "too broadly. Check the cache board's per-namespace ratios — a platform-wide number hides " +
            "which family is responsible.",
            new Dictionary<string, string> { [PlatformTelemetryNames.Attributes.CacheResult] = "hit_local" }),

        new AlertRule(
            "database.latency.high",
            PlatformTelemetryNames.Database.CommandDuration,
            AlertComparison.GreaterThan,
            Threshold: 500,
            For: TimeSpan.FromMinutes(10),
            AlertSeverity.Critical,
            "Platform",
            "Database commands are taking over half a second at the 95th percentile.",
            "Check connection-pool saturation before query plans: a pool that is full makes every " +
            "query look slow because the time is spent waiting for a connection rather than running."),

        new AlertRule(
            "resilience.circuit-open",
            PlatformTelemetryNames.Resilience.CircuitTransitions,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(2),
            AlertSeverity.Critical,
            "Platform",
            "A circuit breaker has opened.",
            "The dependency name is on the metric. An open circuit means calls are being refused " +
            "immediately, which is the platform protecting itself — the question is whether the " +
            "dependency is down or merely slow. Check the provider's own status before resetting; " +
            "resetting into a still-failing dependency just reopens it.",
            new Dictionary<string, string> { [PlatformTelemetryNames.Attributes.Outcome] = "open" }),

        new AlertRule(
            "resilience.retry-storm",
            PlatformTelemetryNames.Resilience.Retries,
            AlertComparison.GreaterThan,
            Threshold: 100,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Warning,
            "Platform",
            "Retries have risen sharply against one dependency.",
            "A retry storm is load the platform is adding to a dependency that is already struggling. " +
            "If the breaker has not opened, its minimum throughput or failure ratio is probably set " +
            "too tolerantly for this dependency."),

        new AlertRule(
            "resilience.bulkhead-rejections",
            PlatformTelemetryNames.Resilience.BulkheadRejections,
            AlertComparison.GreaterThan,
            Threshold: 50,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Warning,
            "Platform",
            "Calls are being refused because a bulkhead is full.",
            "The subsystem named is saturated. This is the platform refusing work rather than " +
            "exhausting its threads on it — check whether the dependency has slowed before raising " +
            "the capacity."),

        new AlertRule(
            "platform.health.unhealthy",
            PlatformTelemetryNames.Health.State,
            AlertComparison.GreaterThan,
            Threshold: 1,
            For: TimeSpan.FromMinutes(2),
            AlertSeverity.Page,
            "Platform",
            "An instance is reporting unhealthy and has been taken out of rotation.",
            "Only a check whose policy says Critical can produce this. The platform-health board names " +
            "which one. If several instances report it at once, look at the shared dependency rather " +
            "than at the instances."),

        new AlertRule(
            "workers.stalled",
            PlatformTelemetryNames.Workers.LastRunAgeSeconds,
            AlertComparison.GreaterThan,
            Threshold: 3_600,
            For: TimeSpan.FromMinutes(15),
            AlertSeverity.Warning,
            "Platform",
            "A background worker has not completed a run in an hour.",
            "Check the workers board for its consecutive-failure count. Three or more means it is in " +
            "backoff with a live error; zero means the loop is not running at all, which is usually a " +
            "worker that was disabled on every instance."),

        new AlertRule(
            "rate-limit.rejections.spike",
            PlatformTelemetryNames.RateLimiting.Rejections,
            AlertComparison.GreaterThan,
            Threshold: 1_000,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Warning,
            "Platform",
            "Rate limiting is refusing an unusual number of calls.",
            "Check the dimension on the metric. A spike on the client-address dimension is usually an " +
            "attack; a spike on the user dimension is usually a client bug retrying in a loop."),

        new AlertRule(
            "security.threat-signals",
            PlatformTelemetryNames.Security.ThreatSignals,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Critical,
            "Platform",
            "A high-severity threat signal was raised.",
            "A signal is an observation and not a verdict — nothing has been blocked. Review the " +
            "subject and the evidence before acting. Signals are pseudonymous by contract; resolving " +
            "one to a member is a deliberate step through Identity.",
            new Dictionary<string, string> { [PlatformTelemetryNames.Attributes.Outcome] = "High" }),

        new AlertRule(
            "security.secrets-expiring",
            PlatformTelemetryNames.Security.ExpiringSecrets,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromHours(1),
            AlertSeverity.Warning,
            "Platform",
            "A secret is due for rotation.",
            "Rotate it through the secret store with a retirement window longer than the lifetime of " +
            "anything signed under it. For the access-token signing key that means longer than the " +
            "access-token expiry, or the rotation signs every member out."),

        new AlertRule(
            "security.audit-gaps",
            PlatformTelemetryNames.Security.AuditGaps,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(30),
            AlertSeverity.Critical,
            "Platform",
            "The audit verifier found records that should exist and do not.",
            "Treat as a correctness incident, not an observability one. A gap means a code path stopped " +
            "writing its audit record; the records already missing cannot be recovered, so the priority " +
            "is stopping the gap from widening."),

        new AlertRule(
            "configuration.validation-failures",
            PlatformTelemetryNames.Configuration.ConfigurationValidationFailures,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(5),
            AlertSeverity.Warning,
            "Platform",
            "A runtime setting holds a value that does not validate.",
            "The setting has fallen back to its declared default, so behaviour is defined — but it is " +
            "not the behaviour somebody configured. The configuration board names the key."),

        new AlertRule(
            "feature-flags.kill-switch",
            PlatformTelemetryNames.Configuration.KillSwitchEngaged,
            AlertComparison.GreaterThan,
            Threshold: 0,
            For: TimeSpan.FromMinutes(1),
            AlertSeverity.Page,
            "Platform",
            "The platform kill switch has been engaged.",
            "Every flag under a policy that permits it is now off. This is always deliberate; the alert " +
            "exists so that everyone learns about it at once rather than from the symptoms."),

        new AlertRule(
            "search.index-lag",
            PlatformTelemetryNames.Search.IndexLagSeconds,
            AlertComparison.GreaterThan,
            Threshold: 3_600,
            For: TimeSpan.FromMinutes(30),
            AlertSeverity.Info,
            "Platform",
            "A search index is more than an hour behind its source.",
            "Search results are stale, not wrong — every search port falls back to its database engine " +
            "when the provider is unavailable, but a lagging provider is available and behind. Rewind " +
            "the index watermark rather than rebuilding if the gap is bounded.")
    ];
}
