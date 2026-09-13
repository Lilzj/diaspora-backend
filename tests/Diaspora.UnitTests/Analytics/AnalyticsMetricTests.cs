using System.Globalization;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// What a served figure means, asserted against the definition it is served with.
/// <para>
/// <b>Every test here corresponds to a sentence in the metric catalogue.</b> §14 of the Phase 7
/// register — "do not invent metric definitions" — is only kept if the arithmetic matches the words,
/// and the only way to know it does is to assert both.
/// </para>
/// </summary>
public sealed class AnalyticsMetricTests
{
    /// <summary>
    /// <b>Every metric the platform serves has a definition, and every definition is a sentence
    /// somebody could read.</b> A metric with a one-word description is a metric the client will show
    /// a member without them learning anything.
    /// </summary>
    [Fact]
    public void Every_metric_carries_a_definition_of_substance()
    {
        AnalyticsMetricCatalog.Definitions.Should().NotBeEmpty();

        foreach (var definition in AnalyticsMetricCatalog.Definitions)
        {
            definition.Definition.Should().NotBeNullOrWhiteSpace();
            definition.Definition.Length.Should().BeGreaterThan(
                20, "{0}'s definition must say what the number counts", definition.Key);
            definition.Label.Should().NotBeNullOrWhiteSpace();
            definition.Owner.Should().NotBeNullOrWhiteSpace();
        }
    }

    /// <summary>Every key a headline set names must exist in the catalogue.</summary>
    [Fact]
    public void Every_served_set_resolves_against_the_catalogue()
    {
        foreach (var key in AnalyticsMetricCatalog.CreatorHeadline
            .Concat(AnalyticsMetricCatalog.ContentItem)
            .Concat(AnalyticsMetricCatalog.CreatorSeries)
            .Concat(AnalyticsMetricCatalog.SellerHeadline))
        {
            AnalyticsMetricCatalog.Find(key).Should().NotBeNull("{0} is served", key);
        }
    }

    /// <summary>
    /// <b>A metric this deployment does not measure says so, and its definition explains why.</b> The
    /// alternative is a zero, and a zero and an unmeasured quantity render identically while meaning
    /// opposite things — which is the failure §21 of the Wave 6 brief recorded for reach.
    /// </summary>
    [Theory]
    [InlineData(AnalyticsMetricCatalog.Reach)]
    [InlineData(AnalyticsMetricCatalog.Revenue)]
    [InlineData(AnalyticsMetricCatalog.ProfileViews)]
    [InlineData(AnalyticsMetricCatalog.Followers)]
    public void An_unmeasured_metric_says_so_rather_than_serving_a_zero(string key)
    {
        var definition = AnalyticsMetricCatalog.Find(key)!;

        definition.Availability.Should().Be(AnalyticsMetricAvailability.NotMeasured);
        definition.Definition.Should().ContainEquivalentOf(
            "not measured", "the definition must say the figure is absent rather than zero");

        var metric = AnalyticsMetricAssembler.Build(key, 0, 0, baselineHasHistory: true);

        metric.IsAvailable.Should().BeFalse();
        metric.ComparisonUnavailableReason.Should().Be("unsupported");
    }

    /// <summary>
    /// <b>The three comparison cases a percentage cannot express, kept apart.</b> A real change is a
    /// number; a zero baseline and an absent baseline are reasons, and every arithmetic answer to
    /// either — 0, infinity, NaN, 100% — is a lie of a different shape.
    /// </summary>
    [Fact]
    public void A_real_baseline_produces_a_percentage()
    {
        var metric = AnalyticsMetricAssembler.Build(
            AnalyticsMetricCatalog.Saves, value: 12, baseline: 10, baselineHasHistory: true);

        metric.ChangePercentage.Should().Be(20);
        metric.PreviousValue.Should().Be(10);
        metric.ComparisonUnavailableReason.Should().BeNull();
    }

    [Fact]
    public void A_zero_baseline_produces_a_reason_rather_than_infinity()
    {
        var metric = AnalyticsMetricAssembler.Build(
            AnalyticsMetricCatalog.Saves, value: 12, baseline: 0, baselineHasHistory: true);

        metric.ChangePercentage.Should().BeNull();
        metric.ComparisonUnavailableReason.Should().Be("zeroBaseline");
        metric.Value.Should().Be(12, "the figure itself is real even when the comparison is not");
    }

    [Fact]
    public void A_subject_with_no_history_says_so_rather_than_reporting_growth()
    {
        var metric = AnalyticsMetricAssembler.Build(
            AnalyticsMetricCatalog.Saves, value: 12, baseline: 0, baselineHasHistory: false);

        metric.ChangePercentage.Should().BeNull();
        metric.PreviousValue.Should().BeNull();
        metric.ComparisonUnavailableReason.Should().Be("insufficientHistory");
    }

    [Fact]
    public void A_fall_is_reported_as_a_negative_percentage()
    {
        var metric = AnalyticsMetricAssembler.Build(
            AnalyticsMetricCatalog.ContentViews, value: 80, baseline: 100, baselineHasHistory: true);

        metric.ChangePercentage.Should().Be(-20);
    }

    /// <summary>
    /// <b>The engagement definition, asserted as arithmetic.</b> Saves, shares, comments and
    /// reactions. Views are the denominator rather than an engagement, and a conversion is an act on
    /// the thing rather than a reaction to it — folding either in would change what the rate means
    /// without changing the sentence beside it.
    /// </summary>
    [Fact]
    public void Engagements_are_saves_shares_comments_and_reactions()
    {
        var counts = new AnalyticsCounts(
            Views: 100,
            Saves: 4,
            Shares: 3,
            Comments: 2,
            Reactions: 1,
            HelpfulVotes: 1,
            Conversions: 50,
            Published: 7,
            FollowsGained: 9,
            FollowsLost: 2);

        counts.Engagements.Should().Be(10, "4 + 3 + 2 + 1, and neither views nor conversions");
    }

    /// <summary>
    /// <b>The engagement rate is engagements over views, expressed as a percentage.</b> The
    /// denominator is views rather than reach, this deployment does not measure reach, and the
    /// definition served with the figure says both.
    /// </summary>
    [Fact]
    public void The_engagement_rate_divides_engagements_by_views()
    {
        var counts = new AnalyticsCounts(200, 10, 5, 4, 1, 0, 0, 0, 0, 0);

        AnalyticsMetricAssembler.EngagementRate(counts).Should().Be(10);

        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.EngagementRate)!
            .Formula.Should().Be("(saves + shares + comments + reactions) ÷ views × 100");
    }

    /// <summary>
    /// <b>A rate with no denominator is undefined, and undefined is not zero.</b> A creator whose work
    /// has not been opened has no engagement <em>rate</em>; a tile reading 0% would tell them their
    /// content is failing to engage the nobody who read it.
    /// </summary>
    [Fact]
    public void The_engagement_rate_is_undefined_when_nothing_was_viewed()
    {
        var counts = new AnalyticsCounts(0, 3, 0, 0, 0, 0, 0, 0, 0, 0);

        AnalyticsMetricAssembler.EngagementRate(counts).Should().BeNull();

        var set = AnalyticsMetricAssembler.BuildSet(
            [AnalyticsMetricCatalog.EngagementRate],
            new AnalyticsWindowTotals(counts, 0, 0, HasData: true, HasHistory: true),
            AnalyticsWindowTotals.Empty);

        set.Single().IsAvailable.Should().BeFalse();
    }

    /// <summary>
    /// <b>Follower growth is a net change, and a net change can be negative.</b> A figure that only
    /// counted follows would report a creator losing an audience as one gaining it.
    /// </summary>
    [Fact]
    public void Follower_growth_subtracts_the_people_who_left()
    {
        var totals = new AnalyticsWindowTotals(
            new AnalyticsCounts(0, 0, 0, 0, 0, 0, 0, 0, FollowsGained: 3, FollowsLost: 8), 0, 0, true, true);

        AnalyticsMetricAssembler
            .ValueOf(AnalyticsMetricCatalog.FollowerGrowth, totals)
            .Should().Be(-5);
    }

    /// <summary>
    /// <b>A gap in the days is a gap in the chart, not a missing point.</b> A series built by walking
    /// the rows rather than the calendar would draw a straight line through a week with no activity
    /// and call it steady.
    /// </summary>
    [Fact]
    public void A_day_with_no_activity_still_produces_a_bucket()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

        var period = AnalyticsPeriod
            .Resolve(AnalyticsPeriodPreset.Last7Days, null, null, TimeZoneInfo.Utc, now, 366)
            .Period!;

        var daily = new Dictionary<DateOnly, double>
        {
            [new DateOnly(2026, 8, 12)] = 5
        };

        var points = AnalyticsMetricAssembler.Bucket(period, daily, new DateOnly(2026, 8, 12));

        points.Should().HaveCount(7);
        points.Take(6).Should().OnlyContain(point => point.Value == 0);
        points[^1].Value.Should().Be(5);
        points[^1].IsPartialBucket.Should().BeTrue("today has not finished");
        points[0].IsPartialBucket.Should().BeFalse("a day in the past is whole");
    }

    /// <summary>
    /// <b>Weekly buckets are anchored at the period start, not at the calendar week.</b> Anchoring at
    /// the calendar would make the <em>first</em> bucket the partial one, which reads on a chart as a
    /// collapse that has already happened rather than a week still in progress.
    /// </summary>
    [Fact]
    public void A_ninety_day_window_is_bucketed_weekly_from_its_own_start()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);

        var period = AnalyticsPeriod
            .Resolve(AnalyticsPeriodPreset.Last90Days, null, null, TimeZoneInfo.Utc, now, 366)
            .Period!;

        var points = AnalyticsMetricAssembler.Bucket(
            period, new Dictionary<DateOnly, double>(), new DateOnly(2026, 8, 12));

        period.Granularity.Should().Be(AnalyticsGranularity.Week);
        points.Should().HaveCount(13, "ninety days is twelve whole weeks and a remainder");
        points[0].At.Should().Be(period.FromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
