using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Fans one usage signal out to every registered sink, and to the module's own meters.
/// <para>
/// <b>A sink that fails must never fail the request that produced the signal.</b> These are recorded
/// on the read path of the platform's most-read screen: a warehouse being briefly unreachable is not
/// a reason to answer a country page with a 500, and an analytics call that can take a page down is
/// an analytics call that will. Failures are logged at error and the request continues, which is the
/// same isolation the section and context registries apply for the same reason.
/// </para>
/// <para>
/// <b>Meters are incremented here rather than in each sink.</b> The counters are about the platform
/// serving the signal, not about any particular destination receiving it — a deployment with no
/// external sink still wants to know how many country pages were opened.
/// </para>
/// </summary>
public sealed class KnowledgeAnalyticsRecorder
{
    private readonly IReadOnlyList<IKnowledgeAnalyticsSink> _sinks;
    private readonly ILogger<KnowledgeAnalyticsRecorder> _logger;

    public KnowledgeAnalyticsRecorder(
        IEnumerable<IKnowledgeAnalyticsSink> sinks,
        ILogger<KnowledgeAnalyticsRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        _sinks = [.. sinks];
        _logger = logger;
    }

    /// <summary>The sinks receiving signals. Reported by the capabilities endpoint.</summary>
    public IReadOnlyList<string> SinkNames => [.. _sinks.Select(sink => sink.Name)];

    public async Task RecordAsync(
        KnowledgeAnalyticsSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        Meter(signal);

        foreach (var sink in _sinks)
        {
            await RecordOneAsync(sink, signal, cancellationToken);
        }
    }

    private async Task RecordOneAsync(
        IKnowledgeAnalyticsSink sink,
        KnowledgeAnalyticsSignal signal,
        CancellationToken cancellationToken)
    {
        try
        {
            await sink.RecordAsync(signal, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A failing sink must not fail the read that produced the signal.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Knowledge analytics sink {Sink} failed for {Kind} in {CountryCode}.",
                sink.Name,
                signal.Kind,
                signal.CountryCode);
        }
#pragma warning restore CA1031
    }

    private static void Meter(KnowledgeAnalyticsSignal signal)
    {
        var country = new KeyValuePair<string, object?>("country", signal.CountryCode ?? "unknown");

        switch (signal.Kind)
        {
            case KnowledgeAnalyticsSignalKinds.DirectoryViewed:
                KnowledgeTelemetry.DirectoryViews.Add(1, country);
                break;

            case KnowledgeAnalyticsSignalKinds.CategoryViewed:
                KnowledgeTelemetry.CategoryViews.Add(1, country);
                break;

            case KnowledgeAnalyticsSignalKinds.GuideDiscovered:
                KnowledgeTelemetry.GuideDiscoveries.Add(1, country);
                break;

            case KnowledgeAnalyticsSignalKinds.Searched:
                KnowledgeTelemetry.Searches.Add(1, country);

                // The one instrument here worth an alert. A rising count is not an error — it is the
                // platform being told, at volume, that people are asking for something nobody has
                // written.
                if (signal.ResultCount == 0)
                {
                    KnowledgeTelemetry.SearchesUnanswered.Add(1, country);
                }

                break;

            default:
                break;
        }
    }
}
