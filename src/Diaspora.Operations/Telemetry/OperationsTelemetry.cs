using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Operations.Telemetry;

namespace Diaspora.Operations.Telemetry;

/// <summary>
/// The Production Platform's instruments.
/// <para>
/// Static, created once, and owned here rather than injected. An <c>ActivitySource</c> and a
/// <c>Meter</c> are designed to be long-lived singletons; resolving them per operation costs more
/// than the measurement and produces a different instrument identity each time, which the exporter
/// reports as a new time series.
/// </para>
/// <para>
/// <b>Nothing here is conditional on an exporter being configured.</b> An unsubscribed instrument
/// is close to free — a counter with no listener does a null check — and making the emission
/// conditional means the day a deployment turns on telemetry, half of it is missing because a flag
/// was read at startup.
/// </para>
/// </summary>
public static class OperationsTelemetry
{
    public static ActivitySource Source { get; } = new(PlatformTelemetryNames.OperationsActivitySource, "1.0.0");

    private static readonly Meter Meter = new(PlatformTelemetryNames.OperationsMeter, "1.0.0");

    // ─── Cache ───────────────────────────────────────────────────────────────────────────
    public static Counter<long> CacheOperations { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Cache.Operations,
        unit: "{operation}",
        description: "Cache reads by result: hit, miss, stale or error.");

    public static Histogram<double> CacheDuration { get; } = Meter.CreateHistogram<double>(
        PlatformTelemetryNames.Cache.Duration,
        unit: "ms",
        description: "Time to answer a cache read, including the factory on a miss.");

    public static Counter<long> CacheInvalidations { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Cache.Invalidations,
        unit: "{invalidation}",
        description: "Invalidations by reason: tag, version, key, event or manual.");

    public static Counter<long> CacheErrors { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Cache.Errors,
        unit: "{error}",
        description: "Cache store faults swallowed. A rising count with a healthy hit ratio means the shared tier is failing and the local tier is hiding it.");

    public static Counter<long> CacheWarmedEntries { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Cache.WarmedEntries,
        unit: "{entry}",
        description: "Entries written by a cache warmer at startup.");

    // ─── Resilience ──────────────────────────────────────────────────────────────────────
    public static Counter<long> Retries { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Resilience.Retries,
        unit: "{retry}",
        description: "Retry attempts, labelled by dependency and policy.");

    public static Counter<long> RetryExhausted { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Resilience.RetryExhausted,
        unit: "{operation}",
        description: "Operations that used every attempt and still failed.");

    public static Counter<long> CircuitTransitions { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Resilience.CircuitTransitions,
        unit: "{transition}",
        description: "Circuit breaker state changes. The open transition is the alertable one.");

    public static Counter<long> BulkheadRejections { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Resilience.BulkheadRejections,
        unit: "{rejection}",
        description: "Calls refused because a bulkhead was full.");

    public static Counter<long> PoisonDetected { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Resilience.PoisonDetected,
        unit: "{message}",
        description: "Messages classified as poison and removed from the retry cycle.");

    // ─── Rate limiting ───────────────────────────────────────────────────────────────────
    public static Counter<long> RateLimitDecisions { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.RateLimiting.Decisions,
        unit: "{decision}",
        description: "Rate-limit decisions by dimension and outcome.");

    public static Counter<long> RateLimitRejections { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.RateLimiting.Rejections,
        unit: "{rejection}",
        description: "Calls refused by a rate limit.");

    // ─── Workers ─────────────────────────────────────────────────────────────────────────
    public static Counter<long> WorkerRuns { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Workers.Runs,
        unit: "{run}",
        description: "Background worker iterations by outcome.");

    public static Histogram<double> WorkerRunDuration { get; } = Meter.CreateHistogram<double>(
        PlatformTelemetryNames.Workers.RunDuration,
        unit: "ms",
        description: "How long one worker iteration took.");

    public static Counter<long> WorkerFailures { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Workers.Failures,
        unit: "{failure}",
        description: "Worker iterations that threw.");

    // ─── Feature flags and configuration ─────────────────────────────────────────────────
    public static Counter<long> FlagEvaluations { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Configuration.FlagEvaluations,
        unit: "{evaluation}",
        description: "Feature flag evaluations by flag and outcome.");

    public static Counter<long> FlagChanges { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Configuration.FlagChanges,
        unit: "{change}",
        description: "Feature flag state changes. Every one of them is a production change.");

    public static Counter<long> KillSwitchEngaged { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Configuration.KillSwitchEngaged,
        unit: "{engagement}",
        description: "Kill switch activations.");

    public static Counter<long> ConfigurationChanges { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Configuration.ConfigurationChanges,
        unit: "{change}",
        description: "Runtime configuration changes by key.");

    public static Counter<long> ConfigurationValidationFailures { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Configuration.ConfigurationValidationFailures,
        unit: "{failure}",
        description: "Runtime configuration values refused by a validator.");

    // ─── Security ────────────────────────────────────────────────────────────────────────
    public static Counter<long> ThreatSignals { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Security.ThreatSignals,
        unit: "{signal}",
        description: "Threat signals raised, by kind and severity.");

    public static Counter<long> SecretRotations { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Security.SecretRotations,
        unit: "{rotation}",
        description: "Secret rotations performed.");

    public static Counter<long> AuditGaps { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Security.AuditGaps,
        unit: "{gap}",
        description: "Audit records the verifier expected and did not find.");

    // ─── Health ──────────────────────────────────────────────────────────────────────────
    public static Histogram<double> HealthCheckDuration { get; } = Meter.CreateHistogram<double>(
        PlatformTelemetryNames.Health.CheckDuration,
        unit: "ms",
        description: "How long one health check took.");

    public static Counter<long> HealthChecks { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Health.Checks,
        unit: "{check}",
        description: "Health check executions by dimension and state.");

    // ─── Search ──────────────────────────────────────────────────────────────────────────
    public static Counter<long> SearchQueries { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Search.Queries,
        unit: "{query}",
        description: "Search queries by provider and index.");

    public static Histogram<double> SearchQueryDuration { get; } = Meter.CreateHistogram<double>(
        PlatformTelemetryNames.Search.QueryDuration,
        unit: "ms",
        description: "Search latency.");

    public static Counter<long> SearchIndexedDocuments { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Search.IndexedDocuments,
        unit: "{document}",
        description: "Documents written to a search index.");

    public static Counter<long> SearchEmptyResults { get; } = Meter.CreateCounter<long>(
        PlatformTelemetryNames.Search.EmptyResults,
        unit: "{query}",
        description: "Searches that matched nothing. B4 named this the signal to watch when the seeded geography is too small.");

    /// <summary>
    /// Registers an observable gauge, once per name for the life of the process.
    /// <para>
    /// <b>The guard is load-bearing and was added after it bit.</b> A <c>Meter</c> is a static
    /// singleton and keeps every callback it is given, along with whatever the closure captures.
    /// Registering a gauge from a component's constructor therefore leaks one callback and one dead
    /// component per instance — invisible in a deployed host, which builds each component once, and
    /// unbounded in the integration suite, which builds a fresh host per test class. The exporter also
    /// reports the same instrument several times, which reads as duplicated time series.
    /// </para>
    /// <para>
    /// The first registration wins and later ones are ignored, which is why callers publish through a
    /// field the gauge reads rather than through a closure over themselves.
    /// </para>
    /// </summary>
    public static void RegisterGaugeOnce<T>(
        string name,
        Func<IEnumerable<Measurement<T>>> observe,
        string? unit = null,
        string? description = null)
        where T : struct
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(observe);

        lock (Gauges)
        {
            if (!Gauges.Add(name))
            {
                return;
            }

            Meter.CreateObservableGauge(name, observe, unit, description);
        }
    }

    private static readonly HashSet<string> Gauges = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers the gauges the platform publishes from static state.
    /// <para>
    /// Called by whichever component owns the number. Kept here rather than in that component so the
    /// callback lives beside the field it reads and neither can be moved without the other.
    /// </para>
    /// </summary>
    public static void RegisterExpiringSecretsGauge() =>
        RegisterGaugeOnce<int>(
            PlatformTelemetryNames.Security.ExpiringSecrets,
            static () => [new Measurement<int>(Volatile.Read(ref _expiringSecrets))],
            unit: "{secret}",
            description: "Secrets past or approaching their rotation date.");

    /// <summary>
    /// Publishes how many secrets are past or approaching their rotation date.
    /// <para>
    /// Behind a method with a private static field rather than a public one, so the gauge's callback
    /// captures nothing at all — which is the point of the whole arrangement — while CA2211's rule
    /// against visible mutable static state still holds.
    /// </para>
    /// </summary>
    public static void ReportExpiringSecrets(int count) =>
        Volatile.Write(ref _expiringSecrets, count);

    private static int _expiringSecrets;
}
