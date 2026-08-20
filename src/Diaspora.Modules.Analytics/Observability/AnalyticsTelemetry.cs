using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Analytics.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>An analytics platform that cannot be observed is one nobody can trust.</b> Every counter here
/// answers a question an operator would otherwise have to answer by reading the database: is
/// ingestion arriving, is any of it being refused and why, how much of it is late, is the projection
/// keeping up, and how far behind the served figures are. The pairing that matters most is
/// <see cref="EventsReceived"/> against <see cref="EventsRejected"/> — a deployment whose rejection
/// rate climbs after a client release has a schema drift, and that is invisible from any
/// infrastructure dashboard.
/// </para>
/// <para>
/// <b>Nothing here carries a payload.</b> §54: "Do not log raw event payloads indiscriminately."
/// Every tag on every counter is a low-cardinality label — an event type, a refusal reason, a trust
/// class — and none of them is a content identifier or an actor.
/// </para>
/// </summary>
public static class AnalyticsTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.Analytics";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    // ── Ingestion ───────────────────────────────────────────────────────────────────────────

    /// <summary>Events offered to ingestion, by trust class and event type.</summary>
    public static Counter<long> EventsReceived { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.events.received", "event", "Analytics events offered to ingestion.");

    /// <summary>Events stored. The difference from received is duplicates plus refusals.</summary>
    public static Counter<long> EventsProcessed { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.events.processed", "event", "Analytics events stored.");

    /// <summary>
    /// Events recognised as already stored.
    /// <para>
    /// Counted rather than ignored, because a duplicate rate that climbs is either a client
    /// retrying too hard or a consumer being redelivered too often, and both are worth seeing before
    /// they become a load problem.
    /// </para>
    /// </summary>
    public static Counter<long> EventsDuplicate { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.events.duplicate", "event", "Analytics events already stored.");

    /// <summary>Events refused, tagged by the reason code. The schema-drift signal.</summary>
    public static Counter<long> EventsRejected { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.events.rejected", "event", "Analytics events refused at ingestion.");

    /// <summary>
    /// Events that arrived for a day the projection had already computed.
    /// <para>
    /// Not an error — late events are expected and handled — but the figure an operator needs to
    /// decide whether the projection's re-read window is wide enough.
    /// </para>
    /// </summary>
    public static Counter<long> EventsLate { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.events.late", "event", "Analytics events for an already-computed day.");

    /// <summary>How far behind an accepted event's occurrence was, in seconds.</summary>
    public static Histogram<double> IngestionLag { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.analytics.ingestion.lag", "s", "Seconds between occurrence and ingestion.");

    // ── Projection ──────────────────────────────────────────────────────────────────────────

    /// <summary>Rollup cells rewritten.</summary>
    public static Counter<long> CellsRebuilt { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.aggregation.cells", "cell", "Rollup cells recomputed.");

    /// <summary>How long one projection pass took.</summary>
    public static Histogram<double> AggregationDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.analytics.aggregation.duration", "ms", "Projection pass duration.");

    /// <summary>Projection passes that threw. The rollups are stale by exactly this much.</summary>
    public static Counter<long> AggregationFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.aggregation.failures", "failure", "Projection passes that failed.");

    /// <summary>Days rebuilt by a backfill. Progress, so a long run is visible while it runs.</summary>
    public static Counter<long> BackfillProgress { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.backfill.progress", "day", "Days rebuilt by a backfill.");

    // ── Query ───────────────────────────────────────────────────────────────────────────────

    /// <summary>How long an analytics read took, tagged by contract.</summary>
    public static Histogram<double> QueryDuration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.analytics.query.duration", "ms", "Analytics query duration.");

    /// <summary>Analytics reads that failed, tagged by error code.</summary>
    public static Counter<long> QueryErrors { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.query.errors", "error", "Analytics reads that failed.");

    // ── Retention and erasure ───────────────────────────────────────────────────────────────

    /// <summary>Rows deleted, tagged by whether the reason was retention or erasure.</summary>
    public static Counter<long> RowsDeleted { get; } =
        Meter.CreateCounter<long>(
            "diaspora.analytics.rows.deleted", "row", "Analytics rows deleted.");
}

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind this name are created in
/// <see cref="AnalyticsTelemetry"/>. Declaring a source without creating one is the B2 mistake — it
/// subscribes the tracer to silence — so both are always done together.
/// </para>
/// </summary>
public sealed class AnalyticsObservabilityContributor : IObservabilityContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [AnalyticsTelemetry.SourceName];

    public IEnumerable<string> Meters => [AnalyticsTelemetry.SourceName];
}
