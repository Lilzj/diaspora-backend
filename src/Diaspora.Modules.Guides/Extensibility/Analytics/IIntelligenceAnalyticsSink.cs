namespace Diaspora.Modules.Guides.Extensibility.Analytics;

/// <summary>
/// Where usage signals from the intelligence surface go.
/// <para>
/// <b>A collection, not last-wins</b> — unlike the search engine, which has to be singular. A
/// deployment legitimately wants its own warehouse <em>and</em> the shipped counters that make the
/// editorial dashboard work with nothing configured, and there is no merge problem: a signal fanned
/// out to three sinks is three independent recordings of the same fact.
/// </para>
/// <para>
/// <b>A sink that throws costs one signal, logged.</b> This is on the read path of the most-read
/// screens in the module, and an analytics failure must never be visible to somebody looking up an
/// emergency number. Signals are best-effort by contract, and every consumer of these numbers should
/// treat them as approximate.
/// </para>
/// </summary>
public interface IIntelligenceAnalyticsSink
{
    /// <summary>The sink's name, for diagnostics and the capabilities endpoint.</summary>
    string Name { get; }

    Task RecordAsync(
        IntelligenceAnalyticsSignal signal, CancellationToken cancellationToken = default);
}
