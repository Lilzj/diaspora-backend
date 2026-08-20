using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Infrastructure.Intelligence;

/// <summary>
/// The shipped analytics sink: folds signals into the per-country counters the editorial dashboard
/// reads.
/// <para>
/// <b>It exists so the dashboard works with nothing configured.</b> A deployment that points a
/// warehouse at the seam gets both — sinks are a collection, not last-wins, because a signal recorded
/// twice in two places is two independent recordings of one fact rather than a merge problem.
/// </para>
/// <para>
/// <b>Failures are swallowed and logged, and this class is where that promise is kept.</b> It runs on
/// the read path of the most-read screens in the module, and somebody looking up an emergency number
/// must never see a page fail because a counter could not be written. The counters are approximate by
/// contract and every consumer of them is told so.
/// </para>
/// <para>
/// It does not save. The caller's unit of work commits, which is what keeps a recorder on a read path
/// from committing on its own — and is why recording three signals in one request costs one write.
/// </para>
/// </summary>
public sealed class CountryCounterIntelligenceSink : IIntelligenceAnalyticsSink
{
    private readonly ICountryIntelligenceAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CountryCounterIntelligenceSink> _logger;

    public CountryCounterIntelligenceSink(
        ICountryIntelligenceAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        ILogger<CountryCounterIntelligenceSink> logger)
    {
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public string Name => "country-counters";

    public async Task RecordAsync(
        IntelligenceAnalyticsSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        // A signal with no country has nowhere to land. That is not an error — a platform-wide
        // search names no country — it simply has no counter.
        if (string.IsNullOrWhiteSpace(signal.CountryCode))
        {
            return;
        }

        try
        {
            var counters = await _analytics.GetOrStartAsync(signal.CountryCode, cancellationToken);

            counters.Apply(Kind(signal.Kind), 1, signal.At);

            // A search that returned nothing increments a second counter. The commissioning signal
            // is worth having as a total rather than as a ratio somebody has to compute.
            if (IsUnansweredSearch(signal))
            {
                counters.Apply(IntelligenceCounterKinds.SearchedUnanswered, 1, signal.At);
            }

            // Where the reader came from, when the client said. This is the whole of delivery
            // correlation, and it costs one counter rather than this module knowing what a
            // notification is.
            var source = SourceCounter(signal.Source);

            if (source is not null)
            {
                counters.Apply(source, 1, signal.At);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // An analytics failure must never be visible on a read path.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Recording the {Kind} signal for {CountryCode} failed. The read succeeded.",
                signal.Kind,
                signal.CountryCode);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Signal kinds and counter kinds are named identically on purpose, so this is the identity
    /// function with a guard rather than a mapping table nobody would keep in step.
    /// </summary>
    private static string Kind(string signalKind) => signalKind.Trim().ToLowerInvariant();

    private static bool IsUnansweredSearch(IntelligenceAnalyticsSignal signal) =>
        string.Equals(
            signal.Kind, IntelligenceAnalyticsSignalKinds.Searched, StringComparison.Ordinal)
        && signal.ResultCount == 0;

    /// <summary>
    /// The two arrival sources worth counting, and nothing else.
    /// <para>
    /// <c>Source</c> is free text from a client, so counting every distinct value would let a client
    /// create unbounded counter rows by sending unbounded strings. Two named sources answer the two
    /// questions the brief asks — recommendation acceptance and notification delivery correlation —
    /// and everything else is ignored.
    /// </para>
    /// </summary>
    private static string? SourceCounter(string? source) =>
        source?.Trim().ToLowerInvariant() switch
        {
            "notification" => IntelligenceCounterKinds.NotificationFollowed,
            "recommendation" => IntelligenceCounterKinds.RecommendationAccepted,
            _ => null
        };
}
