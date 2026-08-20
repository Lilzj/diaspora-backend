using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// What a rule accepts, what it refuses, and how it normalises.
/// <para>
/// The normalisation matters more than it looks: rule equality is what decides whether an edit
/// regenerates a hundred occurrences, so two rules that mean the same thing have to <em>be</em> the
/// same thing. A client sending <c>[3,1,1]</c> and one sending <c>[1,3]</c> must not produce a
/// difference the expander then acts on.
/// </para>
/// </summary>
public class RecurrenceRuleTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    [Fact]
    public void A_day_list_is_sorted_and_de_duplicated()
    {
        var messy = Create(RecurrenceFrequency.Weekly, byWeekday: [3, 1, 1]);
        var tidy = Create(RecurrenceFrequency.Weekly, byWeekday: [1, 3]);

        messy.Value.ByWeekday.Should().Be("1,3");
        messy.Value.Should().Be(tidy.Value, "two rules that mean the same thing are the same rule");
    }

    /// <summary>
    /// A weekday list on a monthly rule is dropped rather than half-honoured.
    /// <para>
    /// A rule carrying both is a rule that means two different things, and the expander can only act
    /// on one of them. Dropping it keeps the stored rule and the generated dates in agreement.
    /// </para>
    /// </summary>
    [Fact]
    public void A_day_list_the_frequency_cannot_use_is_dropped()
    {
        var monthly = Create(
            RecurrenceFrequency.Monthly, byWeekday: [1, 3], byMonthDay: [15]);

        monthly.Value.ByWeekday.Should().BeEmpty();
        monthly.Value.MonthDays.Should().Equal(15);

        var weekly = Create(
            RecurrenceFrequency.Weekly, byWeekday: [1], byMonthDay: [15]);

        weekly.Value.ByMonthDay.Should().BeEmpty();
    }

    /// <summary>ISO numbering: 7 is Sunday, and <see cref="DayOfWeek"/> numbers it zero.</summary>
    [Fact]
    public void Iso_weekday_seven_is_sunday()
    {
        var rule = Create(RecurrenceFrequency.Weekly, byWeekday: [7]);

        rule.Value.Weekdays.Should().Equal(DayOfWeek.Sunday);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(53)]
    public void An_interval_outside_the_bound_is_refused(int interval) =>
        Create(RecurrenceFrequency.Weekly, interval: interval)
            .Error.Code.Should().Be("community-life.recurrence.interval-invalid");

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(-1)]
    public void A_weekday_outside_one_to_seven_is_refused(int day) =>
        Create(RecurrenceFrequency.Weekly, byWeekday: [day])
            .Error.Code.Should().Be("community-life.recurrence.weekday-invalid");

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    [InlineData(-32)]
    public void A_month_day_outside_the_range_is_refused(int day) =>
        Create(RecurrenceFrequency.Monthly, byMonthDay: [day])
            .Error.Code.Should().Be("community-life.recurrence.month-day-invalid");

    /// <summary>An end kind with nothing to end on is refused rather than treated as never.</summary>
    [Fact]
    public void An_until_end_with_no_date_is_refused() =>
        Create(RecurrenceFrequency.Weekly, endKind: RecurrenceEndKind.OnDate)
            .Error.Code.Should().Be("community-life.recurrence.end-invalid");

    [Fact]
    public void A_counted_end_with_no_count_is_refused() =>
        Create(RecurrenceFrequency.Weekly, endKind: RecurrenceEndKind.AfterCount)
            .Error.Code.Should().Be("community-life.recurrence.count-invalid");

    /// <summary>A count past the platform ceiling is refused. One request must not write forever.</summary>
    [Fact]
    public void A_count_past_the_ceiling_is_refused() =>
        Create(
            RecurrenceFrequency.Weekly,
            endKind: RecurrenceEndKind.AfterCount,
            count: 5_000)
            .Error.Code.Should().Be("community-life.recurrence.count-invalid");

    /// <summary>An end value belonging to a different end kind is not stored.</summary>
    [Fact]
    public void An_end_value_the_kind_does_not_use_is_discarded()
    {
        var rule = Create(
            RecurrenceFrequency.Weekly,
            endKind: RecurrenceEndKind.AfterCount,
            count: 5,
            until: new DateOnly(2030, 1, 1));

        rule.Value.Until.Should().BeNull();
        rule.Value.Count.Should().Be(5);
    }

    // ── The server's own words ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Every rule renders a sentence, because the client shows the server's rather than composing
    /// one — the semantics live here, and a sentence written elsewhere drifts from them.
    /// </summary>
    [Theory]
    [InlineData(RecurrenceFrequency.Daily, 1, "Every day")]
    [InlineData(RecurrenceFrequency.Daily, 3, "Every 3 days")]
    [InlineData(RecurrenceFrequency.Weekly, 1, "Every week on Monday")]
    [InlineData(RecurrenceFrequency.Weekly, 2, "Every 2nd week on Monday")]
    public void The_rule_describes_itself(
        RecurrenceFrequency frequency, int interval, string expected) =>
        Create(frequency, interval).Value.Describe(Monday).Should().Be(expected);

    [Fact]
    public void A_counted_rule_says_how_many_times() =>
        Create(RecurrenceFrequency.Weekly, endKind: RecurrenceEndKind.AfterCount, count: 6)
            .Value.Describe(Monday)
            .Should().Be("Every week on Monday, 6 times");

    [Fact]
    public void An_ended_rule_says_when_it_stops() =>
        Create(
            RecurrenceFrequency.Weekly,
            endKind: RecurrenceEndKind.OnDate,
            until: new DateOnly(2026, 12, 25))
            .Value.Describe(Monday)
            .Should().Be("Every week on Monday, until 25 December 2026");

    [Fact]
    public void A_monthly_rule_on_the_last_day_says_so() =>
        Create(RecurrenceFrequency.Monthly, byMonthDay: [-1])
            .Value.Describe(Monday)
            .Should().Be("Every month on the last day");

    // ── The schedule ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_unknown_time_zone_is_refused() =>
        SeriesSchedule.Create(Monday, new TimeOnly(19, 0), 120, "Mars/Olympus")
            .Error.Code.Should().Be("community-life.timezone.invalid");

    [Fact]
    public void A_missing_time_zone_defaults_to_utc() =>
        SeriesSchedule.Create(Monday, new TimeOnly(19, 0), 120, null)
            .Value.TimeZoneId.Should().Be("UTC");

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void A_non_positive_duration_is_refused(int minutes) =>
        SeriesSchedule.Create(Monday, new TimeOnly(19, 0), minutes, "UTC")
            .IsFailure.Should().BeTrue();

    /// <summary>
    /// The local date of an instant is asked in the series' zone, not in UTC. A 23:00 Lagos event
    /// would otherwise report the following day.
    /// </summary>
    [Fact]
    public void The_local_date_of_an_instant_is_read_in_the_series_zone()
    {
        var schedule = SeriesSchedule.Create(
            Monday, new TimeOnly(23, 30), 120, "Africa/Lagos").Value;

        var start = schedule.ToStart(Monday);

        start.UtcDateTime.Date.Should().Be(
            Monday.ToDateTime(new TimeOnly(22, 30)).Date, "22:30 UTC is still the same day");

        schedule.ToLocalDate(start).Should().Be(Monday);
    }

    private static Diaspora.SharedKernel.Results.Result<RecurrenceRule> Create(
        RecurrenceFrequency frequency,
        int interval = 1,
        IReadOnlyList<int>? byWeekday = null,
        IReadOnlyList<int>? byMonthDay = null,
        RecurrenceEndKind endKind = RecurrenceEndKind.Never,
        DateOnly? until = null,
        int? count = null) =>
        RecurrenceRule.Create(
            frequency, interval, byWeekday, byMonthDay, endKind, until, count);
}
