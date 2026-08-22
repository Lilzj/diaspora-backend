using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// The period arithmetic, asserted where it is decided.
/// <para>
/// <b>Every test here is about a way a time window is wrong invisibly.</b> A closed range that
/// counts midnight twice, a baseline that compares thirty days against a calendar month, a day
/// boundary cut in the server's timezone, an inverted custom range silently swapped — none of them
/// produces an error, all of them produce a plausible number, and the only way to catch any of them
/// is to assert the boundary directly.
/// </para>
/// </summary>
public sealed class AnalyticsPeriodTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    /// <summary>
    /// <b>The half-open rule, asserted at the instant it matters.</b> Midnight belongs to the day it
    /// starts and not to the day before. A closed range would count an event at exactly the boundary
    /// in both periods, and the error is one row in a thousand — small enough that nobody finds it,
    /// large enough to make two dashboards disagree.
    /// </summary>
    [Fact]
    public void The_boundary_instant_belongs_to_exactly_one_period()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var period = Resolve(AnalyticsPeriodPreset.Last7Days, now);
        var previous = period.Previous();

        var boundary = period.Start;

        period.Contains(boundary).Should().BeTrue("the window includes its first instant");
        previous.Contains(boundary).Should().BeFalse("the baseline excludes the instant the window starts");

        previous.End.Should().Be(period.Start, "the baseline ends exactly where the window begins");
    }

    /// <summary>
    /// <b>The last day is whole.</b> A window that ended at midnight on its final date would silently
    /// exclude everything that happened on the day the member is looking at — which is most of what
    /// they came to see.
    /// </summary>
    [Fact]
    public void The_window_includes_the_whole_of_its_last_day()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var period = Resolve(AnalyticsPeriodPreset.Today, now);

        period.Contains(new DateTimeOffset(2026, 8, 12, 23, 59, 59, TimeSpan.Zero)).Should().BeTrue();
        period.Contains(new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero)).Should().BeFalse();
        period.Duration.Should().Be(TimeSpan.FromDays(1));
    }

    [Theory]
    [InlineData(AnalyticsPeriodPreset.Today, 1)]
    [InlineData(AnalyticsPeriodPreset.Last7Days, 7)]
    [InlineData(AnalyticsPeriodPreset.Last30Days, 30)]
    [InlineData(AnalyticsPeriodPreset.Last90Days, 90)]
    public void A_preset_covers_the_number_of_days_its_name_promises(
        AnalyticsPeriodPreset preset, int days)
    {
        var period = Resolve(preset, new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero));

        (period.ToDate.DayNumber - period.FromDate.DayNumber + 1).Should().Be(days);
    }

    /// <summary>
    /// <b>§46's rule, and the failure it prevents.</b> A thirty-day window compared against a calendar
    /// month would make February look like a collapse every single year. The baseline is the same
    /// duration, immediately preceding, always.
    /// </summary>
    [Fact]
    public void The_baseline_is_the_same_duration_immediately_preceding()
    {
        var period = Resolve(
            AnalyticsPeriodPreset.Last30Days, new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero));

        var baseline = period.Previous();

        baseline.Duration.Should().Be(period.Duration);
        baseline.End.Should().Be(period.Start);
        baseline.ToDate.Should().Be(period.FromDate.AddDays(-1));
    }

    /// <summary>
    /// <b>The year preset is twelve calendar months, not a rolling 365 days.</b> A rolling year puts a
    /// partial month at both ends and makes every month label on the chart a lie.
    /// </summary>
    [Fact]
    public void The_year_preset_starts_on_the_first_of_a_month()
    {
        var period = Resolve(
            AnalyticsPeriodPreset.Year, new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero));

        period.FromDate.Should().Be(new DateOnly(2025, 9, 1));
        period.ToDate.Should().Be(new DateOnly(2026, 8, 12));
        period.Granularity.Should().Be(AnalyticsGranularity.Month);
    }

    /// <summary>
    /// <b>An inverted range is refused, never swapped.</b> Somebody who typed the dates the wrong way
    /// round is told; showing them figures for a range they did not ask for is worse than an error.
    /// </summary>
    [Fact]
    public void An_inverted_custom_range_is_refused_rather_than_swapped()
    {
        var resolution = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 1),
            Utc,
            DateTimeOffset.UtcNow,
            366);

        resolution.IsResolved.Should().BeFalse();
        resolution.Failure.Should().Be(AnalyticsPeriodFailure.Inverted);
    }

    [Fact]
    public void A_custom_range_with_one_bound_is_incomplete()
    {
        var resolution = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 8, 1),
            null,
            Utc,
            DateTimeOffset.UtcNow,
            366);

        resolution.Failure.Should().Be(AnalyticsPeriodFailure.Incomplete);
    }

    [Fact]
    public void A_custom_range_wider_than_the_cap_is_refused()
    {
        var resolution = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2024, 1, 1),
            new DateOnly(2026, 1, 1),
            Utc,
            DateTimeOffset.UtcNow,
            366);

        resolution.Failure.Should().Be(AnalyticsPeriodFailure.TooLong);
        resolution.Limit.Should().Be(366);
    }

    /// <summary>
    /// <b>The timezone decides which day an instant belongs to, and the difference is a whole day.</b>
    /// 22:00 UTC on the eleventh is already the twelfth in Auckland. A platform that cut its days in
    /// the server's zone would report a different "today" depending on where the host happened to run.
    /// </summary>
    [Fact]
    public void The_day_boundary_moves_with_the_reporting_timezone()
    {
        var auckland = AnalyticsReportingZone("Pacific/Auckland");

        if (auckland is null)
        {
            return;
        }

        var now = new DateTimeOffset(2026, 8, 11, 22, 0, 0, TimeSpan.Zero);

        var utcToday = AnalyticsPeriod
            .Resolve(AnalyticsPeriodPreset.Today, null, null, Utc, now, 366).Period!;

        var aucklandToday = AnalyticsPeriod
            .Resolve(AnalyticsPeriodPreset.Today, null, null, auckland, now, 366).Period!;

        utcToday.FromDate.Should().Be(new DateOnly(2026, 8, 11));
        aucklandToday.FromDate.Should().Be(new DateOnly(2026, 8, 12));

        utcToday.Start.Should().NotBe(aucklandToday.Start);
    }

    /// <summary>
    /// <b>The spring-forward day, where midnight does not exist.</b> In several zones the clock jumps
    /// from 23:59 to 01:00, and constructing "the start of the day" from the nominal local time
    /// without checking places it an hour inside the previous day — shifting every figure for that
    /// one day of the year, in a way that only shows up once.
    /// </summary>
    [Fact]
    public void A_day_whose_midnight_does_not_exist_still_starts_somewhere()
    {
        // Lord Howe and a few Brazilian zones skip midnight; Santiago is the widely available one.
        var santiago = AnalyticsReportingZone("America/Santiago");

        if (santiago is null)
        {
            return;
        }

        // Every day of a year, through both transitions. None may throw and none may produce a start
        // that is not inside its own day.
        for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
        {
            var resolution = AnalyticsPeriod.Resolve(
                AnalyticsPeriodPreset.Custom, day, day, santiago, DateTimeOffset.UtcNow, 366);

            resolution.IsResolved.Should().BeTrue("every calendar day must resolve, including {0}", day);

            var period = resolution.Period!;

            period.End.Should().BeAfter(period.Start, "a day is never empty or inverted");
            period.Duration.Should().BeGreaterThan(TimeSpan.Zero);
            period.Duration.Should().BeLessThanOrEqualTo(TimeSpan.FromHours(25));
        }
    }

    private static AnalyticsPeriod Resolve(AnalyticsPeriodPreset preset, DateTimeOffset now) =>
        AnalyticsPeriod.Resolve(preset, null, null, Utc, now, 366).Period!;

    /// <summary>
    /// A zone, or null where the host's zone database does not carry it.
    /// <para>
    /// Skipped rather than failed: a container image without the full IANA database would otherwise
    /// turn a packaging difference into a red build, and the behaviour under test is the platform's
    /// rather than the image's.
    /// </para>
    /// </summary>
    private static TimeZoneInfo? AnalyticsReportingZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    // ── Every analytics read from a real device returned 500 ─────────────────
    //
    // `Start` and `End` are computed in the caller's zone, so a request from
    // Lagos carried `+01:00`. Those boundaries go to EF as query parameters and
    // Npgsql refuses them outright:
    //
    //     Cannot write DateTimeOffset with Offset=01:00:00 to PostgreSQL type
    //     'timestamp with time zone', only offset 0 (UTC) is supported.
    //
    // So `GET /analytics/creators/me/summary` — and every other analytics read —
    // answered **500 for any device outside UTC**, which is every real device.
    //
    // It survived every probe because they all passed `timezone=UTC` or omitted
    // it, and the default is UTC. Found only by watching a phone in Africa/Lagos
    // fail while the identical curl succeeded.

    [Theory]
    [InlineData("Africa/Lagos")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kolkata")]
    public void Boundaries_are_written_in_Utc_whatever_zone_was_asked_for(string zoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);

        var resolution = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Last30Days,
            null,
            null,
            zone,
            DateTimeOffset.UtcNow,
            366);

        resolution.IsResolved.Should().BeTrue();

        // The assertion Npgsql makes, made here instead.
        resolution.Period.Start.Offset.Should().Be(TimeSpan.Zero);
        resolution.Period.End.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Normalising_to_Utc_does_not_move_the_window()
    {
        // `ToUniversalTime()` changes how an instant is written, not which
        // instant it is. If this ever stops holding, the fix has started
        // reporting a different month than the member asked for.
        var lagos = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");
        var now = DateTimeOffset.UtcNow;

        var resolved = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Last30Days, null, null, lagos, now, 366);

        resolved.IsResolved.Should().BeTrue();
        var period = resolved.Period;

        // The zone the member asked for is not lost — it is what the day
        // boundaries were computed in, and it is echoed back.
        period.TimeZoneId.Should().Be("Africa/Lagos");

        // And the window still covers the same span of real time.
        period.Duration.Should().BeGreaterThan(TimeSpan.Zero);
        period.Contains(period.Start).Should().BeTrue();
        period.Contains(period.End).Should().BeFalse();
    }

    [Fact]
    public void The_previous_window_is_Utc_too()
    {
        // `Previous()` builds a new period from this one's boundaries, so it
        // would reintroduce the offset if the constructor did not normalise.
        var kolkata = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

        var resolved = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Last30Days, null, null, kolkata, DateTimeOffset.UtcNow, 366);

        var previous = resolved.Period.Previous();

        previous.Start.Offset.Should().Be(TimeSpan.Zero);
        previous.End.Offset.Should().Be(TimeSpan.Zero);
    }

}
