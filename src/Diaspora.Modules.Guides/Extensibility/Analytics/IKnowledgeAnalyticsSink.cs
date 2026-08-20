namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// Where a directory usage signal goes.
/// <para>
/// <b>A collection, not a last-wins registration, and that is the difference between this and the
/// search seam.</b> There is one right answer to a search and many legitimate destinations for a
/// signal: the built-in counters that make the dashboard work, plus whatever warehouse or product
/// analytics a deployment runs. Every registered sink receives every signal.
/// </para>
/// <para>
/// <b>A sink must not throw and must not be slow.</b> These are recorded on the read path of the
/// platform's most-read screen. The recorder isolates failures — a sink that throws is logged and
/// skipped — but a sink that blocks on a network call is a latency regression on every country page,
/// so an external one should buffer and flush rather than send inline.
/// </para>
/// </summary>
public interface IKnowledgeAnalyticsSink
{
    /// <summary>The sink's name, for diagnostics and the failure log.</summary>
    string Name { get; }

    Task RecordAsync(
        KnowledgeAnalyticsSignal signal, CancellationToken cancellationToken = default);
}
