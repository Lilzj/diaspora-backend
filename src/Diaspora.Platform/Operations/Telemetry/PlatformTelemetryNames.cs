namespace Diaspora.Platform.Operations.Telemetry;

/// <summary>
/// Every metric, span and attribute name the Production Platform emits.
/// <para>
/// Constants in one file for a reason that has nothing to do with tidiness: a metric name is a
/// contract with a dashboard and an alert rule that live outside this repository. Renaming
/// <c>diaspora.cache.hits</c> is a breaking change to somebody's pager, and it is a breaking change
/// with no compiler error and no test failure unless the names are declared once and referenced.
/// </para>
/// <para>
/// <b>Every metric carries <see cref="Attributes.Module"/>.</b> That is the phase brief's
/// requirement and it is also the only way a platform of fifteen modules can answer "which module
/// is causing this" from a dashboard rather than from a trace search.
/// </para>
/// </summary>
public static class PlatformTelemetryNames
{
    /// <summary>The meter the Production Platform's own instruments belong to.</summary>
    public const string OperationsMeter = "Diaspora.Operations";

    /// <summary>The activity source the Production Platform's own spans belong to.</summary>
    public const string OperationsActivitySource = "Diaspora.Operations";

    /// <summary>Attribute keys. Shared by spans, metrics and log scopes so the three join up.</summary>
    public static class Attributes
    {
        /// <summary>The owning module. On every instrument, without exception.</summary>
        public const string Module = "diaspora.module";

        /// <summary>The operation within the module — a command name, a queue name, a cache namespace.</summary>
        public const string Operation = "diaspora.operation";

        /// <summary>Success, failure, cancelled, rejected. Low cardinality by construction.</summary>
        public const string Outcome = "diaspora.outcome";

        /// <summary>The operational policy that governed the operation, with its version.</summary>
        public const string Policy = "diaspora.policy";

        /// <summary>The correlation id, propagated from the inbound request.</summary>
        public const string CorrelationId = "diaspora.correlation_id";

        /// <summary>The request kind: command, query, event, job.</summary>
        public const string RequestKind = "diaspora.request.kind";

        /// <summary>Which tier or provider answered — <c>memory</c>, <c>redis</c>, <c>database</c>.</summary>
        public const string Provider = "diaspora.provider";

        /// <summary>Cache result: hit, miss, stale, error.</summary>
        public const string CacheResult = "diaspora.cache.result";

        /// <summary>The cache namespace.</summary>
        public const string CacheNamespace = "diaspora.cache.namespace";

        /// <summary>The queue or worker name.</summary>
        public const string Queue = "diaspora.queue";

        /// <summary>The dependency a breaker or retry protects.</summary>
        public const string Dependency = "diaspora.dependency";

        /// <summary>The rate-limit dimension that refused a call.</summary>
        public const string LimitDimension = "diaspora.limit.dimension";

        /// <summary>The health dimension a check reports on.</summary>
        public const string HealthDimension = "diaspora.health.dimension";

        /// <summary>The reason a retry, invalidation or refusal happened. Enumerated, never free text.</summary>
        public const string Reason = "diaspora.reason";

        /// <summary>The environment name. Set once as a resource attribute, not per measurement.</summary>
        public const string Environment = "deployment.environment.name";
    }

    /// <summary>Request-level instruments. Emitted by the CQRS pipeline and the HTTP layer.</summary>
    public static class Requests
    {
        public const string Handled = "diaspora.requests.handled";
        public const string Duration = "diaspora.requests.duration";
        public const string Failed = "diaspora.requests.failed";
        public const string InFlight = "diaspora.requests.in_flight";
    }

    /// <summary>Cache instruments. The hit ratio is derived from hits and misses by the dashboard.</summary>
    public static class Cache
    {
        public const string Operations = "diaspora.cache.operations";
        public const string Duration = "diaspora.cache.duration";
        public const string Invalidations = "diaspora.cache.invalidations";
        public const string Errors = "diaspora.cache.errors";
        public const string Entries = "diaspora.cache.entries";
        public const string WarmedEntries = "diaspora.cache.warmed_entries";
    }

    /// <summary>Messaging and queue instruments.</summary>
    public static class Messaging
    {
        public const string QueueDepth = "diaspora.queue.depth";
        public const string OldestAgeSeconds = "diaspora.queue.oldest_age";
        public const string Processed = "diaspora.queue.processed";
        public const string ProcessingDuration = "diaspora.queue.processing_duration";
        public const string DeadLetters = "diaspora.queue.dead_letters";
        public const string DeadLetterReplays = "diaspora.queue.dead_letter_replays";
        public const string ConsumerConcurrency = "diaspora.queue.consumer_concurrency";
        public const string Shed = "diaspora.queue.shed";
    }

    /// <summary>Resilience instruments.</summary>
    public static class Resilience
    {
        public const string Retries = "diaspora.resilience.retries";
        public const string RetryExhausted = "diaspora.resilience.retry_exhausted";
        public const string CircuitState = "diaspora.resilience.circuit_state";
        public const string CircuitTransitions = "diaspora.resilience.circuit_transitions";
        public const string BulkheadRejections = "diaspora.resilience.bulkhead_rejections";
        public const string BulkheadOccupancy = "diaspora.resilience.bulkhead_occupancy";
        public const string PoisonDetected = "diaspora.resilience.poison_detected";
    }

    /// <summary>Rate-limiting instruments.</summary>
    public static class RateLimiting
    {
        public const string Decisions = "diaspora.rate_limit.decisions";
        public const string Rejections = "diaspora.rate_limit.rejections";
        public const string QueueLength = "diaspora.rate_limit.queue_length";
    }

    /// <summary>Database instruments. Emitted by the persistence interceptor.</summary>
    public static class Database
    {
        public const string CommandDuration = "diaspora.database.command_duration";
        public const string Commands = "diaspora.database.commands";
        public const string Failures = "diaspora.database.failures";
        public const string SaveChanges = "diaspora.database.save_changes";
        public const string ConnectionsOpened = "diaspora.database.connections_opened";
    }

    /// <summary>Background worker instruments.</summary>
    public static class Workers
    {
        public const string Runs = "diaspora.worker.runs";
        public const string RunDuration = "diaspora.worker.run_duration";
        public const string Failures = "diaspora.worker.failures";
        public const string Active = "diaspora.worker.active";
        public const string LastRunAgeSeconds = "diaspora.worker.last_run_age";
    }

    /// <summary>Feature-flag and configuration instruments.</summary>
    public static class Configuration
    {
        public const string FlagEvaluations = "diaspora.feature_flag.evaluations";
        public const string FlagChanges = "diaspora.feature_flag.changes";
        public const string KillSwitchEngaged = "diaspora.feature_flag.kill_switch_engaged";
        public const string ConfigurationChanges = "diaspora.configuration.changes";
        public const string ConfigurationValidationFailures = "diaspora.configuration.validation_failures";
    }

    /// <summary>Security instruments.</summary>
    public static class Security
    {
        public const string ThreatSignals = "diaspora.security.threat_signals";
        public const string SecretRotations = "diaspora.security.secret_rotations";
        public const string ExpiringSecrets = "diaspora.security.expiring_secrets";
        public const string AuditVerifications = "diaspora.security.audit_verifications";
        public const string AuditGaps = "diaspora.security.audit_gaps";
    }

    /// <summary>Health instruments.</summary>
    public static class Health
    {
        /// <summary>0 healthy, 1 degraded, 2 unhealthy. Numeric so it can be graphed and alerted on.</summary>
        public const string State = "diaspora.health.state";

        public const string CheckDuration = "diaspora.health.check_duration";
        public const string Checks = "diaspora.health.checks";
    }

    /// <summary>Search instruments. Emitted by whichever search provider is registered.</summary>
    public static class Search
    {
        public const string Queries = "diaspora.search.queries";
        public const string QueryDuration = "diaspora.search.query_duration";
        public const string IndexedDocuments = "diaspora.search.indexed_documents";
        public const string IndexLagSeconds = "diaspora.search.index_lag";
        public const string EmptyResults = "diaspora.search.empty_results";
    }
}
