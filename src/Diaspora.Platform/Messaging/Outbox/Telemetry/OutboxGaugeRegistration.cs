using System.Diagnostics.Metrics;

namespace Diaspora.Platform.Messaging.Outbox.Telemetry;

/// <summary>
/// Attaches the observable gauges to the shared snapshot.
/// <para>
/// Observable instruments run their callback on scrape, on the metrics library's thread, with
/// no scope and no cancellation token. Reading a database there is how a metrics endpoint
/// becomes an outage; every callback here reads one field of an already-computed record.
/// </para>
/// <para>
/// Called once at startup. The gauges are held so the meter does not collect them — an
/// observable instrument with no live reference stops reporting, silently, some time after the
/// first garbage collection, and the symptom is a dashboard that was working last week.
/// </para>
/// </summary>
public sealed class OutboxGaugeRegistration
{
    private readonly List<object> _instruments = [];

    public OutboxGaugeRegistration(OutboxStatisticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.depth",
            () => snapshot.Current.Pending,
            unit: "{message}",
            description: "Messages written and not yet delivered."));

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.claimed",
            () => snapshot.Current.Claimed,
            unit: "{message}",
            description: "Messages currently leased by a worker."));

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.retry_backlog",
            () => snapshot.Current.Retrying,
            unit: "{message}",
            description: "Pending messages that have already failed at least once."));

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.dead_letters",
            () => snapshot.Current.DeadLettered,
            unit: "{message}",
            description: "Dead letters currently retained."));

        // The signal that distinguishes a deep queue from a stopped one.
        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.oldest_pending_age",
            () => snapshot.Current.OldestPendingAge.TotalSeconds,
            unit: "s",
            description: "Age of the oldest undelivered message."));

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.module_depth",
            () => snapshot.Current.ByModule.Select(module =>
                new Measurement<int>(module.Pending, new KeyValuePair<string, object?>("module", module.Module))),
            unit: "{message}",
            description: "Undelivered messages per producing module."));

        _instruments.Add(OutboxTelemetry.Meter.CreateObservableGauge(
            "diaspora.outbox.event_type_depth",
            () => snapshot.Current.ByEventType.Select(type =>
                new Measurement<int>(type.Pending, new KeyValuePair<string, object?>("event.type", type.EventType))),
            unit: "{message}",
            description: "Undelivered messages per event contract."));
    }

    /// <summary>
    /// The registered instruments, held only so they stay reachable. Exposed so a test can
    /// assert the set rather than reaching into the meter.
    /// </summary>
    public IReadOnlyList<object> Instruments => _instruments;
}
