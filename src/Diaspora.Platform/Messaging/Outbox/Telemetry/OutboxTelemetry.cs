using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Platform.Messaging.Outbox.Telemetry;

/// <summary>
/// The outbox's instruments.
/// <para>
/// <b>Chosen for the questions an operator asks during an incident, not for the ones that are
/// easy to count.</b> "Messages published" is a throughput number that looks identical whether
/// the queue is empty or a hundred thousand deep. The numbers that distinguish those are the
/// depth gauges and, above all, the age of the oldest pending message — the single signal that
/// separates a busy outbox from a stopped one.
/// </para>
/// <para>
/// The gauges read a snapshot refreshed by the dispatcher rather than querying on scrape. A
/// metrics endpoint scraped every fifteen seconds by every instance in the cluster, each running
/// five aggregates over the hot table, is a load generator wearing an observability badge.
/// </para>
/// </summary>
public static class OutboxTelemetry
{
    public const string SourceName = "Diaspora.Platform.Outbox";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Events written into the outbox, tagged by module and event type.</summary>
    public static readonly Counter<long> Enqueued = Meter.CreateCounter<long>(
        "diaspora.outbox.enqueued",
        unit: "{message}",
        description: "Integration events written into the outbox inside a producer's transaction.");

    /// <summary>Events delivered, tagged by module, event type and transport.</summary>
    public static readonly Counter<long> Published = Meter.CreateCounter<long>(
        "diaspora.outbox.published",
        unit: "{message}",
        description: "Messages a transport accepted.");

    /// <summary>
    /// Publish failures, tagged by event type and exception type.
    /// <para>
    /// The exception tag is what makes this actionable: a wall of <c>TimeoutException</c> is a
    /// dependency, a wall of <c>InvalidOperationException</c> is a consumer bug, and an
    /// undifferentiated failure count reads the same in both cases.
    /// </para>
    /// </summary>
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "diaspora.outbox.publish_failures",
        unit: "{message}",
        description: "Publish attempts a transport rejected, tagged by exception type.");

    /// <summary>Retries scheduled. Divided by publishes, how much of the throughput is rework.</summary>
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>(
        "diaspora.outbox.retries",
        unit: "{message}",
        description: "Delivery attempts rescheduled after a failure.");

    /// <summary>
    /// Messages given up on, tagged by reason. Should be zero, and an alert on any non-zero
    /// value is one of the few alerts in this platform that is never noise.
    /// </summary>
    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>(
        "diaspora.outbox.dead_lettered",
        unit: "{message}",
        description: "Messages moved to the dead-letter table, tagged by reason.");

    /// <summary>
    /// Leases reclaimed from workers that stopped without releasing them.
    /// <para>
    /// A steadily non-zero rate means instances are dying mid-publish — visible here and
    /// essentially nowhere else, because every individual message still gets delivered.
    /// </para>
    /// </summary>
    public static readonly Counter<long> LeasesRecovered = Meter.CreateCounter<long>(
        "diaspora.outbox.leases_recovered",
        unit: "{message}",
        description: "Expired leases returned to the pending pool after a worker died.");

    /// <summary>Rows removed by the retention sweep, tagged by population.</summary>
    public static readonly Counter<long> Purged = Meter.CreateCounter<long>(
        "diaspora.outbox.purged",
        unit: "{row}",
        description: "Rows deleted by the retention sweep, tagged by population.");

    /// <summary>Dead letters put back on the queue by an operator.</summary>
    public static readonly Counter<long> Replayed = Meter.CreateCounter<long>(
        "diaspora.outbox.replayed",
        unit: "{message}",
        description: "Dead letters re-enqueued by an operator.");

    /// <summary>How long one publish took, tagged by event type and outcome.</summary>
    public static readonly Histogram<double> PublishDuration = Meter.CreateHistogram<double>(
        "diaspora.outbox.publish_duration",
        unit: "ms",
        description: "Time a single transport publish took.");

    /// <summary>How long one dispatch pass took, including claiming.</summary>
    public static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "diaspora.outbox.dispatch_duration",
        unit: "ms",
        description: "Time one dispatcher pass took, from claim to last completion.");

    /// <summary>
    /// Time from a message being committed to it being delivered.
    /// <para>
    /// The number a product owner cares about, and the only one that answers "how long after
    /// somebody publishes a post does the notification go out". Distinct from publish duration,
    /// which measures the transport and would look excellent while the queue was hours behind.
    /// </para>
    /// </summary>
    public static readonly Histogram<double> QueueLatency = Meter.CreateHistogram<double>(
        "diaspora.outbox.queue_latency",
        unit: "ms",
        description: "Elapsed time between a message being enqueued and delivered.");
}
