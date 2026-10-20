using Diaspora.Modules.Analytics.Application.Queries;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// Cohort retention and the insight rules — the arithmetic behind <c>/analytics/retention</c> and
/// <c>/analytics/insights</c>, pinned where it is decided.
/// </summary>
public sealed class AnalyticsCohortAndInsightTests
{
    // 2026-10-19 is a Monday.
    private static readonly DateOnly Monday = new(2026, 10, 19);

    [Fact]
    public void A_cohort_is_the_people_first_seen_in_a_week_and_retention_is_their_return()
    {
        var weekAgo = Monday.AddDays(-7);

        var activity = new List<(string, DateOnly)>
        {
            // Two people first seen the week before; one comes back this week.
            ("ada", weekAgo.AddDays(1)),
            ("ada", Monday.AddDays(2)),
            ("bola", weekAgo.AddDays(3)),

            // One person first seen this week.
            ("chi", Monday)
        };

        var table = AnalyticsCohortTable.Build(
            activity, AnalyticsCohortGranularity.Week, 2, Monday.AddDays(3), weekAgo.AddDays(-30));

        table.Unavailable.Should().BeEmpty();
        table.Cohorts.Should().HaveCount(2);

        var earlier = table.Cohorts[0];
        earlier.Start.Should().Be(weekAgo);
        earlier.Size.Should().Be(2);
        earlier.Values[0].Should().Be(1);
        earlier.Values[1].Should().Be(0.5, "one of the two came back the next week");
        earlier.Values[2].Should().BeNull("the week after has not begun by the end day");
        earlier.IsIncomplete.Should().BeTrue();

        var latest = table.Cohorts[1];
        latest.Start.Should().Be(Monday);
        latest.Size.Should().Be(1, "ada and bola were already seen and are not new this week");
    }

    [Fact]
    public void A_cohort_before_the_kept_history_is_listed_unavailable_never_counted()
    {
        var horizon = Monday.AddDays(-3);

        var table = AnalyticsCohortTable.Build(
            [("ada", Monday)], AnalyticsCohortGranularity.Week, 3, Monday, horizon);

        table.Unavailable.Select(gap => gap.Start).Should().Equal(Monday.AddDays(-14), Monday.AddDays(-7));
        table.Cohorts.Should().ContainSingle().Which.Start.Should().Be(Monday);
    }

    [Fact]
    public void An_empty_cohort_carries_no_percentages_rather_than_zeroes()
    {
        var table = AnalyticsCohortTable.Build(
            [], AnalyticsCohortGranularity.Day, 1, Monday, Monday.AddDays(-10));

        var cohort = table.Cohorts.Should().ContainSingle().Subject;

        cohort.Size.Should().Be(0);
        cohort.Values.Should().OnlyContain(value => value == null);
    }

    [Fact]
    public void Months_start_on_the_first_and_weeks_on_monday()
    {
        AnalyticsCohortTable.PeriodStart(new DateOnly(2026, 10, 25), AnalyticsCohortGranularity.Week)
            .Should().Be(Monday, "25 October 2026 is a Sunday");

        AnalyticsCohortTable.PeriodStart(new DateOnly(2026, 10, 25), AnalyticsCohortGranularity.Month)
            .Should().Be(new DateOnly(2026, 10, 1));

        AnalyticsCohortTable.OffsetLabels(AnalyticsCohortGranularity.Month)
            .Should().Equal("Month 0", "Month 1", "Month 2", "Month 3", "Month 4", "Month 5");
    }

    [Fact]
    public void A_change_is_not_stated_over_too_few_views()
    {
        GetAnalyticsInsightsQueryHandler.ViewsChange(50, 9, 30, Baseline(), "your work")
            .Should().BeNull("nine views in the earlier period is too few to call a percentage");
    }

    [Fact]
    public void A_change_is_stated_with_its_figures_and_a_small_one_holds_steady()
    {
        var rose = GetAnalyticsInsightsQueryHandler.ViewsChange(30, 20, 30, Baseline(), "your work")!;

        rose.Key.Should().Be("views-rose");
        rose.Headline.Should().Be("Views rose 50% against the previous 30 days");
        rose.Tone.Should().Be("positive");
        rose.Detail.Should().Contain("30 views").And.Contain("against 20");

        GetAnalyticsInsightsQueryHandler.ViewsChange(102, 100, 30, Baseline(), "your work")!
            .Key.Should().Be("views-steady");

        GetAnalyticsInsightsQueryHandler.ViewsChange(10, 100, 30, Baseline(), "your work")!
            .Tone.Should().Be("negative");
    }

    [Fact]
    public void A_ratio_needs_enough_views_and_at_least_one_action()
    {
        GetAnalyticsInsightsQueryHandler.Ratio("saves-per-view", "save", 3, 19, Baseline()).Should().BeNull();
        GetAnalyticsInsightsQueryHandler.Ratio("saves-per-view", "save", 0, 500, Baseline()).Should().BeNull();

        GetAnalyticsInsightsQueryHandler.Ratio("enquiries-per-view", "enquiry", 4, 100, Baseline())!
            .Headline.Should().Be("About one enquiry for every 25 views");
    }

    private static AnalyticsPeriod Baseline() =>
        AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            366).Period!;
}
