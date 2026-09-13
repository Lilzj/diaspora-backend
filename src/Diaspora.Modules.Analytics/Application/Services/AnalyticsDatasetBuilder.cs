using System.Globalization;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// Assembles the shared Phase 15 dataset from figures already computed.
/// <para>
/// <b>It translates and never calculates.</b> The Phase 7 metric shape and the Phase 15 dataset are
/// two renderings of one set of numbers, and the numbers are produced once by
/// <see cref="AnalyticsMetricAssembler"/>. A builder that recomputed anything would be a second
/// place a definition lives, which is the failure FRONTEND-PHASE15-GAP-030 exists to prevent.
/// </para>
/// <para>
/// <b>The percentage-to-ratio conversion happens here and nowhere else.</b> Phase 7 publishes
/// <c>changePercentage: 18</c> meaning eighteen percent; the Phase 15 model's <c>changePercent</c> is
/// a <em>ratio</em>, 0.18. A hundredfold error that looks plausible in both directions, so it is
/// done once, in one method, with the reason written beside it.
/// </para>
/// </summary>
public sealed class AnalyticsDatasetBuilder
{
    private readonly AnalyticsReportingCalendar _calendar;
    private readonly AnalyticsOptions _options;

    public AnalyticsDatasetBuilder(
        AnalyticsReportingCalendar calendar, IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _calendar = calendar;
        _options = options.Value;
    }

    public AnalyticsDatasetResponse Build(
        AnalyticsSubjectResponse subject,
        AnalyticsPeriod period,
        AnalyticsPeriod baseline,
        IReadOnlyList<CreatorMetricResponse> metrics,
        IReadOnlyList<AnalyticsSeriesResponse> series,
        DateTimeOffset? dataAsOf,
        IReadOnlyList<AnalyticsLeaderboardResponse> leaderboards)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(metrics);

        var freshness = new DataFreshnessResponse
        {
            ComputedAt = dataAsOf ?? _calendar.UtcNow,
            SourceName = "Diaspora analytics",
            Methodology = string.Create(
                CultureInfo.InvariantCulture,
                $"Counted from recorded platform activity and recomputed about every {_options.ProjectionIntervalSeconds} seconds. Days are cut in {period.TimeZoneId}."),
            Limitations = Limitations(metrics)
        };

        var served = metrics.Where(metric => metric.IsAvailable).ToList();
        var withheld = metrics.Where(metric => !metric.IsAvailable).ToList();

        return new AnalyticsDatasetResponse
        {
            Subject = subject,
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = AnalyticsQueryContext.ToDatasetPreset(period.Preset),
                From = period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                To = period.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Timezone = period.TimeZoneId,

                // Never the device's. This platform publishes a reporting zone, so the client never
                // has to fall back and never has to say it did.
                IsDeviceTimezone = false
            },
            Comparison = "previousPeriod",

            // PARTIAL rather than LIVE whenever anything was asked for and could not be answered.
            // §37's last line: flattening a mixed dataset to one status either marks real figures as
            // simulated or simulated ones as real.
            Status = withheld.Count == 0 ? "LIVE" : "PARTIAL",
            Metrics = served
                .Select(metric => ToDatasetMetric(metric, baseline, freshness))
                .ToList(),
            Series = series,
            Breakdowns = [],
            Funnels = [],
            Leaderboards = leaderboards,
            Insights = [],
            Freshness = freshness,
            UnavailableMetrics = withheld
                .Select(metric => AnalyticsMetricCatalog.Find(metric.Key)?.CatalogueKey ?? metric.Key)
                .ToList()
        };
    }

    /// <summary>Turns a Phase 7 metric into the Phase 15 shape.</summary>
    private static AnalyticsMetricResponse ToDatasetMetric(
        CreatorMetricResponse metric, AnalyticsPeriod baseline, DataFreshnessResponse freshness)
    {
        var definition = AnalyticsMetricCatalog.Find(metric.Key);

        return new AnalyticsMetricResponse
        {
            Key = definition?.CatalogueKey ?? metric.Key,
            Label = metric.Label,
            Value = new SourcedValueResponse { Value = metric.Value, Source = "backend" },
            Format = metric.Format,

            // Never `estimated`: every figure this deployment serves is a count of stored facts.
            // A sketch or a model would say so here, and there is none.
            Computation = "server-aggregate",
            HigherIsBetter = metric.HigherIsBetter,
            Comparison = new AnalyticsComparisonResponse
            {
                Basis = "previousPeriod",
                BaselineFrom = baseline.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                BaselineTo = baseline.ToDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                BaselineValue = metric.PreviousValue,

                // Percent to ratio. See the class summary for why this happens exactly once.
                ChangePercent = metric.ChangePercentage / 100d,
                ChangeAbsolute = metric.PreviousValue is null
                    ? null
                    : metric.Value - metric.PreviousValue,
                UnavailableReason = metric.ComparisonUnavailableReason
            },
            Freshness = freshness,
            PrivacyClass = AnalyticsMetricAssembler.PrivacyName(
                definition?.Privacy ?? Domain.Enums.AnalyticsPrivacyClass.Owner),

            // Nothing served here is suppressed, because nothing served here is a breakdown over a
            // population. A creator's own totals are about their own work; the suppression rule bites
            // on country and community breakdowns, which this phase does not serve.
            IsSuppressed = false
        };
    }

    /// <summary>
    /// What the dataset cannot tell the reader, in the platform's words.
    /// <para>
    /// Rendered verbatim beside the figures. A dashboard that showed only what it could measure and
    /// said nothing about what it could not would leave a creator to assume the silence meant zero.
    /// </para>
    /// </summary>
    /// <summary>
    /// What the dataset could not answer, in sentences a member can take at their word.
    /// <para>
    /// <b>Two different reasons, and until 13 Sep 2026 they shared one sentence.</b> A metric can be
    /// withheld because this deployment does not measure it at all — revenue, reach — or because it
    /// is a rate and there was nothing in the period to calculate it from. Every withheld metric was
    /// reported as "Not measured on this deployment", so a seller with no views this week read that
    /// Diaspora does not measure their conversion rate. It does; it had nothing to divide by.
    /// </para>
    /// <para>
    /// The catalogue already knows which is which, so this asks it rather than adding a reason to the
    /// wire. A definition marked <see cref="AnalyticsMetricAvailability.NotMeasured"/> is the first
    /// kind. Anything else withheld is the second: the only way a measured metric is withheld is a
    /// rate whose denominator was zero — see <c>AnalyticsMetricAssembler.BuildSet</c>.
    /// </para>
    /// <para>
    /// Neither sentence ever offers a zero in place of the figure. A 0% conversion rate for a listing
    /// nobody opened would tell a seller it does not sell, when it has not been seen.
    /// </para>
    /// </summary>
    private static List<string>? Limitations(IReadOnlyList<CreatorMetricResponse> metrics)
    {
        var withheld = metrics.Where(metric => !metric.IsAvailable).ToList();

        if (withheld.Count == 0)
        {
            return null;
        }

        var notMeasured = withheld
            .Where(metric => AnalyticsMetricCatalog.Find(metric.Key)?.Availability
                is AnalyticsMetricAvailability.NotMeasured or null)
            .Select(metric => metric.Label)
            .ToList();

        var nothingToCalculate = withheld
            .Where(metric => AnalyticsMetricCatalog.Find(metric.Key)?.Availability
                is not (AnalyticsMetricAvailability.NotMeasured or null))
            .Select(metric => metric.Label)
            .ToList();

        var sentences = new List<string>(2);

        if (notMeasured.Count > 0)
        {
            sentences.Add($"Not measured on this deployment: {string.Join(", ", notMeasured)}.");
        }

        if (nothingToCalculate.Count > 0)
        {
            sentences.Add(
                $"No figure yet for {string.Join(", ", nothingToCalculate)}: "
                + "nothing was viewed in this period, so there is nothing to work it out from.");
        }

        return sentences;
    }

    /// <summary>One series in the Phase 15 shape, from a daily map.</summary>
    public static IReadOnlyList<AnalyticsSeriesResponse> SeriesFrom(
        AnalyticsPeriod period,
        IReadOnlyDictionary<DateOnly, double> daily,
        string metricKey,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(period);

        var definition = AnalyticsMetricCatalog.Find(metricKey);

        if (definition is null)
        {
            return [];
        }

        var points = AnalyticsMetricAssembler.Bucket(period, daily, today);

        return
        [
            new AnalyticsSeriesResponse
            {
                Key = definition.CatalogueKey,
                Label = definition.Label,
                Granularity = AnalyticsMetricAssembler.GranularityName(period.Granularity),
                Format = AnalyticsMetricAssembler.FormatName(definition.Format),
                Points = points
                    .Select(point => new AnalyticsPointResponse
                    {
                        At = point.At,
                        Value = point.Value,
                        IsPartialBucket = point.IsPartialBucket
                    })
                    .ToList(),
                Source = "backend"
            }
        ];
    }
}
