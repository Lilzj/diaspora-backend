using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Fans one usage signal out to every registered sink, and to the module's own meters.
/// <para>
/// <b>Nothing here can fail a read.</b> Every sink is wrapped, every failure is logged, and the
/// method returns. This runs on the path that serves emergency numbers, and an analytics outage that
/// took that page down would be an incident caused entirely by wanting to count something.
/// </para>
/// <para>
/// <b>Sinks are a collection rather than last-wins</b>, unlike the search engine. A deployment
/// legitimately wants its own warehouse and the shipped counters that make the editorial dashboard
/// work with nothing configured, and there is no merge problem: one signal recorded in three places
/// is three independent recordings of one fact.
/// </para>
/// <para>
/// The meters are incremented here rather than in a sink, because telemetry is not analytics: an
/// operator watching <c>intelligence.emergency.views</c> climb needs it whether or not a deployment
/// has configured any sink at all.
/// </para>
/// </summary>
public sealed class IntelligenceAnalyticsRecorder
{
    private readonly IReadOnlyList<IIntelligenceAnalyticsSink> _sinks;
    private readonly IOptions<IntelligenceOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<IntelligenceAnalyticsRecorder> _logger;

    public IntelligenceAnalyticsRecorder(
        IEnumerable<IIntelligenceAnalyticsSink> sinks,
        IOptions<IntelligenceOptions> options,
        IClock clock,
        ILogger<IntelligenceAnalyticsRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(sinks);

        _sinks = [.. sinks];
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>The sinks registered, by name. For the capabilities endpoint.</summary>
    public IReadOnlyList<string> SinkNames => [.. _sinks.Select(sink => sink.Name)];

    /// <summary>Records one signal. Never throws, and never fails the caller.</summary>
    public async Task RecordAsync(
        IntelligenceAnalyticsSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        Meter(signal);

        if (!_options.Value.RecordAnalytics || _sinks.Count == 0)
        {
            return;
        }

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.RecordAsync(signal, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A sink failure must never be visible on a read path.
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "The {Sink} analytics sink failed on a {Kind} signal. The read succeeded.",
                    sink.Name,
                    signal.Kind);
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>Builds a signal stamped with the current instant.</summary>
    public IntelligenceAnalyticsSignal Signal(
        string kind,
        string? countryCode = null,
        Guid? noticeId = null,
        string? categoryKey = null,
        Guid? embassyId = null,
        Guid? visaAdvisoryId = null,
        Guid? guideId = null,
        string? term = null,
        int? resultCount = null,
        string? source = null,
        string? locale = null) =>
        new()
        {
            Kind = kind,
            CountryCode = countryCode,
            OfficialNoticeId = noticeId,
            CategoryKey = categoryKey,
            EmbassyId = embassyId,
            VisaAdvisoryId = visaAdvisoryId,
            GuideId = guideId,
            Term = term,
            ResultCount = resultCount,
            Source = source,
            Locale = locale,
            At = _clock.UtcNow
        };

    /// <summary>
    /// The instruments an operator watches, incremented whether or not any sink is configured.
    /// <para>
    /// Only the signals worth an operational chart are here. Counting every kind would give a meter
    /// per signal and a dashboard nobody reads; the ones that made the cut are the ones that say
    /// something about the platform rather than about editorial performance.
    /// </para>
    /// </summary>
    private static void Meter(IntelligenceAnalyticsSignal signal)
    {
        var country = new KeyValuePair<string, object?>("country", signal.CountryCode);

        switch (signal.Kind)
        {
            case IntelligenceAnalyticsSignalKinds.NoticeViewed:
                IntelligenceTelemetry.NoticeViews.Add(1, country);
                break;

            case IntelligenceAnalyticsSignalKinds.DashboardViewed:
                IntelligenceTelemetry.DashboardViews.Add(1, country);
                break;

            case IntelligenceAnalyticsSignalKinds.EmergencyViewed:
                IntelligenceTelemetry.EmergencyViews.Add(1, country);
                break;

            case IntelligenceAnalyticsSignalKinds.Searched:
                IntelligenceTelemetry.Searches.Add(1, country);

                if (signal.ResultCount == 0)
                {
                    IntelligenceTelemetry.SearchesUnanswered.Add(1, country);
                }

                break;

            default:
                break;
        }
    }
}
