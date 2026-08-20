using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Application.Observability;

/// <summary>
/// The application layer's real telemetry instruments.
/// <para>
/// B2 shipped three <c>const string</c> source names and registered them with the tracer.
/// No <c>ActivitySource</c> existed behind any of them, so the pipeline subscribed to
/// sources that could never emit and the platform had exactly zero custom spans. These are
/// live instruments; the contributor beside this file is what subscribes them.
/// </para>
/// <para>
/// One source and one meter for the whole CQRS pipeline, tagged by request name, rather
/// than one per module. Cardinality lives in the tags, where a backend can aggregate it;
/// a source per module would fragment every dashboard.
/// </para>
/// </summary>
public static class ApplicationTelemetry
{
    public const string SourceName = "Diaspora.Application";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");

    public static readonly Meter Meter = new(SourceName, "1.0.0");

    /// <summary>Requests handled, tagged with the request name and outcome.</summary>
    public static readonly Counter<long> RequestsHandled =
        Meter.CreateCounter<long>(
            "diaspora.requests",
            unit: "{request}",
            description: "CQRS requests handled, by request name and outcome.");

    /// <summary>End-to-end handler duration, tagged with the request name and outcome.</summary>
    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>(
            "diaspora.request.duration",
            unit: "ms",
            description: "Time spent inside the CQRS pipeline, by request name and outcome.");

    /// <summary>
    /// Transactions committed or rolled back, tagged with the request name and outcome.
    /// Worth its own instrument: a rollback rate that moves is the earliest signal of a
    /// class of bug that logs alone will not surface.
    /// </summary>
    public static readonly Counter<long> Transactions =
        Meter.CreateCounter<long>(
            "diaspora.transactions",
            unit: "{transaction}",
            description: "Database transactions opened by the pipeline, by outcome.");
}
