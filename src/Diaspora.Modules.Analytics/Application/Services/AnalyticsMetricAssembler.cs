using System.Globalization;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// A window's figures, ready to be turned into metrics.
/// <para>
/// <b>Two flags, because one could not tell a quiet window from an unrecorded one.</b> The
/// projection writes a row only for a day on which something happened, so "no row in this window"
/// is true of a subject analytics had never recorded and equally of one that had a quiet spell.
/// Until 13 Sep 2026 <c>HasData</c> alone decided the comparison, and a seller with a fortnight of
/// history was told "Not enough history yet" on every tile of a day that followed a quiet one.
/// </para>
/// <para>
/// <c>HasData</c> is whether this window has rows. <c>HasHistory</c> is whether analytics had
/// recorded anything for the subject by this window's last day — in it or before it. The second is
/// what separates "nothing happened" from "we were not recording yet", which §7 requires to be
/// representable.
/// </para>
/// </summary>
/// <param name="Counts">The additive counts, summed across the window's days.</param>
/// <param name="UniqueViewers">Distinct viewers across the whole window, from the raw store.</param>
/// <param name="ProfileViews">Profile opens across the window.</param>
/// <param name="HasData">Whether any rollup row exists for this subject in this window.</param>
/// <param name="HasHistory">
/// Whether any rollup row exists for this subject on or before this window's last day.
/// </param>
public sealed record AnalyticsWindowTotals(
    AnalyticsCounts Counts,
    long UniqueViewers,
    long ProfileViews,
    bool HasData,
    bool HasHistory)
{
    /// <summary>
    /// Always true where <see cref="HasData"/> is: a window with rows has history by definition, so
    /// a caller that passes only "recorded before the window" still gets the whole answer.
    /// </summary>
    public bool HasHistory { get; init; } = HasData || HasHistory;

    public static AnalyticsWindowTotals Empty => new(AnalyticsCounts.Zero, 0, 0, false, false);
}

/// <summary>
/// Turns totals into served metrics.
/// <para>
/// <b>Every rule about what a number means lives here, once.</b> The comparison arithmetic, the
/// zero-denominator handling, the availability decision and the definition lookup are one
/// implementation shared by the creator overview, the per-item rows and the Phase 15 dataset — so a
/// figure cannot mean one thing on a tile and another on the same tile's drill-down.
/// </para>
/// </summary>
public static class AnalyticsMetricAssembler
{
    /// <summary>
    /// Builds a metric.
    /// <para>
    /// <b>Three cases the comparison must keep apart, and a percentage can only express one of
    /// them.</b> A baseline of five and a current of six is <c>+20%</c>. A baseline of zero with
    /// activity now is not "infinite growth" — it is a ratio with no denominator, and every
    /// arithmetic answer to it (0, ∞, NaN, 100%) is a lie of a different shape. And a subject
    /// analytics had recorded nothing for by the end of the baseline has no baseline at all, which
    /// is a different statement again. The first is a number; the other two are reasons.
    /// </para>
    /// <para>
    /// <b>A quiet baseline is the second case, not the third.</b> A subject with earlier activity
    /// whose comparison period happened to be empty has a baseline, and it is zero.
    /// <paramref name="baselineHasHistory"/> is therefore asked about everything up to the baseline's
    /// last day, never about the baseline window alone — see <see cref="AnalyticsWindowTotals"/>.
    /// </para>
    /// </summary>
    public static CreatorMetricResponse Build(
        string key,
        double value,
        double baseline,
        bool baselineHasHistory,
        bool isMeasured = true)
    {
        var definition = AnalyticsMetricCatalog.Find(key)
            ?? throw new ArgumentOutOfRangeException(
                nameof(key), key, "Every served metric must have a catalogue definition.");

        var available = isMeasured
            && definition.Availability != AnalyticsMetricAvailability.NotMeasured;

        double? change = null;
        double? previous = null;
        string? reason = null;

        if (!available)
        {
            // An unmeasured metric has nothing to compare. Saying "unsupported" rather than leaving
            // the reason null is what stops a client rendering an empty comparison chip.
            reason = "unsupported";
        }
        else if (!baselineHasHistory)
        {
            reason = "insufficientHistory";
        }
        else if (baseline <= 0)
        {
            previous = 0;
            reason = "zeroBaseline";
        }
        else
        {
            previous = baseline;
            change = Math.Round((value - baseline) / baseline * 100d, 2, MidpointRounding.AwayFromZero);
        }

        return new CreatorMetricResponse
        {
            Key = definition.Key,
            Label = definition.Label,
            Value = available ? value : 0,
            Format = FormatName(definition.Format),
            ChangePercentage = change,
            PreviousValue = previous,
            ComparisonUnavailableReason = reason,
            HigherIsBetter = definition.HigherIsBetter,
            Definition = definition.Definition,
            IsAvailable = available
        };
    }

    /// <summary>
    /// The engagement rate for a window.
    /// <para>
    /// <b>Undefined when there are no views, and undefined is not zero.</b> A creator whose work has
    /// not been opened has no engagement <em>rate</em> — the denominator is missing, not small — and
    /// a tile reading "0%" would tell them their content is failing to engage the nobody who read
    /// it. The metric is served as unmeasured for that window and the client renders the definition.
    /// </para>
    /// </summary>
    public static double? EngagementRate(AnalyticsCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        return counts.Views <= 0
            ? null
            : Math.Round(counts.Engagements / (double)counts.Views * 100d, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Actions taken per view, as a percentage, or null when nothing was viewed.
    /// <para>
    /// The same zero-denominator rule as <see cref="EngagementRate"/>: a listing nobody opened has
    /// no conversion rate, and reporting 0% would tell a seller their listing fails to sell when it
    /// has simply not been seen.
    /// </para>
    /// </summary>
    public static double? ConversionRate(AnalyticsCounts counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        return counts.Views <= 0
            ? null
            : Math.Round(counts.Conversions / (double)counts.Views * 100d, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The value a metric key takes from a window's totals.</summary>
    public static double ValueOf(string key, AnalyticsWindowTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);

        return key switch
        {
            AnalyticsMetricCatalog.ContentViews => totals.Counts.Views,
            AnalyticsMetricCatalog.UniqueViewers => totals.UniqueViewers,
            AnalyticsMetricCatalog.Saves => totals.Counts.Saves,
            AnalyticsMetricCatalog.Shares => totals.Counts.Shares,
            AnalyticsMetricCatalog.Comments => totals.Counts.Comments,
            AnalyticsMetricCatalog.HelpfulVotes => totals.Counts.HelpfulVotes,
            AnalyticsMetricCatalog.Conversion => totals.Counts.Conversions,
            AnalyticsMetricCatalog.ContentPublished => totals.Counts.Published,
            AnalyticsMetricCatalog.FollowerGrowth =>
                totals.Counts.FollowsGained - totals.Counts.FollowsLost,
            AnalyticsMetricCatalog.ProfileViews => totals.ProfileViews,
            AnalyticsMetricCatalog.EngagementRate => EngagementRate(totals.Counts) ?? 0,
            AnalyticsMetricCatalog.ConversionRate => ConversionRate(totals.Counts) ?? 0,
            _ => 0
        };
    }

    /// <summary>
    /// Builds the metric list for a subject's headline set.
    /// <para>
    /// The order is the catalogue's, so two dashboards showing the same subject lay their tiles out
    /// the same way and a member switching period does not watch the tiles reshuffle.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CreatorMetricResponse> BuildSet(
        IReadOnlyList<string> keys,
        AnalyticsWindowTotals current,
        AnalyticsWindowTotals baseline)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(baseline);

        var metrics = new List<CreatorMetricResponse>(keys.Count);

        foreach (var key in keys)
        {
            // A rate with no denominator is not measured — the `?? 0` in `ValueOf` only gives the
            // unavailable metric something to carry, and this flag is what stops it being shown.
            var measured = key switch
            {
                AnalyticsMetricCatalog.EngagementRate => EngagementRate(current.Counts) is not null,
                AnalyticsMetricCatalog.ConversionRate => ConversionRate(current.Counts) is not null,
                _ => true
            };

            metrics.Add(Build(
                key,
                ValueOf(key, current),
                ValueOf(key, baseline),
                baseline.HasHistory,
                measured));
        }

        return metrics;
    }

    /// <summary>Which of a served set this deployment cannot answer, for <c>unavailableMetrics</c>.</summary>
    public static IReadOnlyList<string> UnavailableIn(IReadOnlyList<CreatorMetricResponse> metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        return metrics.Where(metric => !metric.IsAvailable).Select(metric => metric.Key).ToList();
    }

    public static string FormatName(AnalyticsMetricFormat format) => format switch
    {
        AnalyticsMetricFormat.Percentage => "percentage",
        AnalyticsMetricFormat.Currency => "currency",
        AnalyticsMetricFormat.Duration => "duration",
        _ => "count"
    };

    public static string GranularityName(AnalyticsGranularity granularity) => granularity switch
    {
        AnalyticsGranularity.Hour => "hour",
        AnalyticsGranularity.Week => "week",
        AnalyticsGranularity.Month => "month",
        _ => "day"
    };

    public static string PrivacyName(AnalyticsPrivacyClass privacy) => privacy switch
    {
        AnalyticsPrivacyClass.Public => "public",
        AnalyticsPrivacyClass.Administrative => "administrative",
        AnalyticsPrivacyClass.Financial => "financial",
        _ => "owner"
    };

    /// <summary>
    /// Cuts a daily series into the period's buckets.
    /// <para>
    /// <b>The server decides the boundaries and the client never regroups.</b> §45: a browser that
    /// grouped dates would group them in the device's calendar, which is a different calendar from
    /// the one the figures were counted in — and the two would disagree by a day for a third of the
    /// world.
    /// </para>
    /// <para>
    /// Buckets are anchored at the period's start rather than at a calendar boundary, so a
    /// ninety-day window's first bucket is a whole week and its last is whatever is left. Anchoring
    /// at the calendar instead would make the <em>first</em> bucket the partial one, which reads on
    /// a chart as a collapse that has already happened rather than a week still in progress.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CreatorSeriesPointResponse> Bucket(
        AnalyticsPeriod period,
        IReadOnlyDictionary<DateOnly, double> daily,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(period);
        ArgumentNullException.ThrowIfNull(daily);

        var points = new List<CreatorSeriesPointResponse>();
        var cursor = period.FromDate;

        while (cursor <= period.ToDate)
        {
            var next = period.Granularity switch
            {
                AnalyticsGranularity.Week => cursor.AddDays(7),
                AnalyticsGranularity.Month => cursor.AddMonths(1),
                _ => cursor.AddDays(1)
            };

            var end = next > period.ToDate.AddDays(1) ? period.ToDate.AddDays(1) : next;

            var total = 0d;

            for (var day = cursor; day < end; day = day.AddDays(1))
            {
                if (daily.TryGetValue(day, out var value))
                {
                    total += value;
                }
            }

            points.Add(new CreatorSeriesPointResponse
            {
                At = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Value = total,

                // A bucket is partial when the day after its last day has not finished — which is
                // true of today's bucket on every dashboard, every day.
                IsPartialBucket = next > today
            });

            cursor = next;
        }

        return points;
    }
}
