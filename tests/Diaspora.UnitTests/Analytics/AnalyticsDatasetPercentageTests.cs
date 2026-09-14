using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// A percentage crosses the Phase 15 wire as a ratio — 14 Sep 2026.
/// <para>
/// <b>Found with a second account.</b> David opened Aisha's job once and applied, and her Hiring
/// dashboard read "Application rate 10,000%". The assembler works the rate out as a whole percentage,
/// 100, which is right for the Phase 7 shape. The builder converted only the comparison to a ratio and
/// passed the 100 through, and the app formats every percentage as a ratio. Every rate a test or a
/// walkthrough had drawn before was zero or withheld — and zero is the one value on which the two
/// conventions agree.
/// </para>
/// </summary>
public sealed class AnalyticsDatasetPercentageTests
{
    private static readonly string[] Keys =
    [
        AnalyticsMetricCatalog.ContentViews,
        AnalyticsMetricCatalog.Conversion,
        AnalyticsMetricCatalog.ConversionRate,
        AnalyticsMetricCatalog.EngagementRate
    ];

    [Fact]
    public void One_application_for_one_view_is_sent_as_one()
    {
        var rate = Metric(Build(Totals(views: 1, conversions: 1), Totals(views: 0, conversions: 0)), "conversion.rate");

        rate.Format.Should().Be("percentage");
        rate.Value.Value.Should().Be(1, "one hundred percent, as a ratio");
    }

    [Fact]
    public void Its_baseline_and_the_difference_are_ratios_too()
    {
        // One application from one view now, one from two views before: 100% against 50%.
        var rate = Metric(Build(Totals(views: 1, conversions: 1), Totals(views: 2, conversions: 1)), "conversion.rate");

        rate.Comparison!.BaselineValue.Should().Be(0.5);
        rate.Comparison.ChangeAbsolute.Should().Be(0.5, "fifty percentage points, drawn in the rate's own format");
        rate.Comparison.ChangePercent.Should().Be(1, "the rate doubled: a relative change, converted once");
    }

    [Fact]
    public void The_engagement_rate_follows_the_same_rule()
    {
        // One save from four views.
        var rate = Metric(Build(Totals(views: 4, conversions: 0, saves: 1), Totals(views: 0, conversions: 0)), "content.engagementRate");

        rate.Value.Value.Should().Be(0.25);
    }

    [Fact]
    public void A_count_is_left_as_it_is()
    {
        var views = Metric(Build(Totals(views: 3, conversions: 1), Totals(views: 2, conversions: 1)), "content.views");

        views.Value.Value.Should().Be(3);
        views.Comparison!.BaselineValue.Should().Be(2);
        views.Comparison.ChangeAbsolute.Should().Be(1);
        views.Comparison.ChangePercent.Should().Be(0.5);
    }

    [Fact]
    public void The_phase_7_shape_keeps_its_whole_percentages()
    {
        // The creator summary route serves the assembler's metric as it is, and the app divides it there.
        // Converting in the assembler instead would break that route in the opposite direction.
        AnalyticsMetricAssembler
            .BuildSet(Keys, Totals(views: 1, conversions: 1), Totals(views: 0, conversions: 0))
            .Single(metric => metric.Key == AnalyticsMetricCatalog.ConversionRate)
            .Value.Should().Be(100);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AnalyticsMetricResponse Metric(AnalyticsDatasetResponse response, string catalogueKey) =>
        response.Metrics.Single(metric => metric.Key == catalogueKey);

    private static AnalyticsWindowTotals Totals(long views, long conversions, long saves = 0) =>
        new(
            new AnalyticsCounts(views, saves, 0, 0, 0, 0, conversions, 0, 0, 0, 0),
            UniqueViewers: views,
            ProfileViews: 0,
            HasData: true,
            HasHistory: true);

    private static AnalyticsDatasetResponse Build(AnalyticsWindowTotals current, AnalyticsWindowTotals baseline)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions());
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero));

        var builder = new AnalyticsDatasetBuilder(new AnalyticsReportingCalendar(clock, options), options);

        var period = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 9, 14),
            new DateOnly(2026, 9, 14),
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
            366).Period!;

        return builder.Build(
            new AnalyticsSubjectResponse { Type = "employer", Id = null, Label = null },
            period,
            period.Previous(),
            AnalyticsMetricAssembler.BuildSet(Keys, current, baseline),
            [],
            null,
            []);
    }
}
