using Diaspora.Contracts.Automation.V1.Extensibility;
using System.Globalization;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// Turning a member's wall clock into an instant, which is where every scheduling defect lives.
/// <para>
/// <b>The two days a year the conversion is not a function are the whole test.</b> When the clocks go
/// forward a wall-clock time does not exist; when they go back it happens twice. .NET throws on the
/// first and silently picks one on the second, and both defaults are wrong for a reminder somebody
/// set — one loses the run, the other makes two instances disagree about when it was.
/// </para>
/// <para>
/// <c>Europe/London</c> is used throughout because its transitions are at a known local hour and the
/// identifiers resolve on every platform this deployment runs on.
/// </para>
/// </summary>
public sealed class AutomationScheduleTests
{
    // ── Timezone identifiers ────────────────────────────────────────────────────────────────

    /// <summary>IANA identifiers resolve. An offset is never accepted as one.</summary>
    [Theory]
    [InlineData("Africa/Lagos", true)]
    [InlineData("Europe/London", true)]
    [InlineData("America/New_York", true)]
    [InlineData("UTC", true)]
    [InlineData("+01:00", false)]
    [InlineData("Middle/Earth", false)]
    public void Only_real_time_zones_resolve(string id, bool known) =>
        AutomationScheduleCalculator.IsKnownTimeZone(id).Should().Be(known);

    /// <summary>An unresolvable zone produces no occurrence and says so, rather than defaulting to UTC.</summary>
    [Fact]
    public void An_unknown_zone_produces_no_occurrence()
    {
        var automation = Daily("Middle/Earth", 9, 0);

        var plan = AutomationScheduleCalculator.Next(automation, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        plan.NextRunAt.Should().BeNull();
        plan.Reason.Should().Contain("Middle/Earth");
    }

    // ── Daily ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A daily schedule lands on the member's wall clock, not the server's.
    /// <para>
    /// 09:00 Africa/Lagos is 08:00 UTC, and Lagos does not observe daylight saving, so it is 08:00 UTC
    /// in January and in July. That invariance is the point: a member in Lagos who sets 09:00 gets
    /// 09:00 all year.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("2026-01-15T00:00:00Z", "2026-01-15T08:00:00Z")]
    [InlineData("2026-07-15T00:00:00Z", "2026-07-15T08:00:00Z")]
    public void A_daily_schedule_lands_on_the_members_wall_clock(string now, string expected)
    {
        var plan = AutomationScheduleCalculator.Next(Daily("Africa/Lagos", 9, 0), DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().Be(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture));
    }

    /// <summary>A time already past today lands tomorrow, not today.</summary>
    [Fact]
    public void A_time_already_past_lands_tomorrow()
    {
        var plan = AutomationScheduleCalculator.Next(
            Daily("Africa/Lagos", 9, 0),
            DateTimeOffset.Parse("2026-01-15T12:00:00Z", CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().Be(DateTimeOffset.Parse("2026-01-16T08:00:00Z", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The same wall clock is a different instant in summer where the zone observes daylight saving.
    /// <para>
    /// 09:00 Europe/London is 09:00 UTC in January and 08:00 UTC in July. A schedule stored as an
    /// offset would be an hour wrong for half the year, and nothing would report it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("2026-01-15T00:00:00Z", "2026-01-15T09:00:00Z")]
    [InlineData("2026-07-15T00:00:00Z", "2026-07-15T08:00:00Z")]
    public void Daylight_saving_moves_the_instant_and_not_the_wall_clock(string now, string expected)
    {
        var plan = AutomationScheduleCalculator.Next(Daily("Europe/London", 9, 0), DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().Be(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture));
    }

    // ── The two transitions ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A wall clock inside the spring-forward gap moves past it, with a warning.
    /// <para>
    /// In 2026 the United Kingdom's clocks go forward at 01:00 on 29 March, so 01:30 does not exist.
    /// A member whose 01:30 daily reminder falls in the missing hour wants it at 02:30 that day, not
    /// silently skipped and not an exception in a worker.
    /// </para>
    /// </summary>
    [Fact]
    public void A_wall_clock_in_the_spring_gap_moves_forward_with_a_warning()
    {
        var plan = AutomationScheduleCalculator.Next(
            Daily("Europe/London", 1, 30),
            DateTimeOffset.Parse("2026-03-29T00:00:00Z", CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().NotBeNull();
        plan.Warnings.Should().ContainSingle().Which.Should().Contain("does not exist");
    }

    /// <summary>
    /// A wall clock that happens twice takes the first, with a warning.
    /// <para>
    /// In 2026 the clocks go back at 02:00 on 25 October, so 01:30 happens twice. The earlier offset
    /// is taken and it is <i>stated</i> rather than left to whichever .NET picks, so two instances
    /// computing the same schedule agree — and the member is told, because a reminder that arrives an
    /// hour before they expected is the surprise §42 exists to prevent.
    /// </para>
    /// </summary>
    [Fact]
    public void A_wall_clock_that_happens_twice_takes_the_first_with_a_warning()
    {
        var plan = AutomationScheduleCalculator.Next(
            Daily("Europe/London", 1, 30),
            DateTimeOffset.Parse("2026-10-25T00:00:00Z", CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().Be(
            DateTimeOffset.Parse("2026-10-25T00:30:00Z", CultureInfo.InvariantCulture),
            "the earlier offset is British Summer Time, one hour ahead of UTC");

        plan.Warnings.Should().ContainSingle().Which.Should().Contain("twice");
    }

    // ── Weekly and monthly ──────────────────────────────────────────────────────────────────

    /// <summary>A weekly schedule reaches its own weekday next week when today has passed.</summary>
    [Fact]
    public void A_weekly_schedule_reaches_next_week_when_today_has_passed()
    {
        var automation = Draft();
        automation.SetSchedule(
            AutomationFrequency.Weekly, AutomationTimezoneMode.UserSelected, "Africa/Lagos",
            9, 0, [4], null, null, null, null, null, null, null);

        // Thursday 15 January 2026, 12:00 Lagos — the 09:00 slot has gone.
        var plan = AutomationScheduleCalculator.Next(automation, DateTimeOffset.Parse("2026-01-15T11:00:00Z", CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().Be(DateTimeOffset.Parse("2026-01-22T08:00:00Z", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A day-of-month that does not exist is clamped by the server, and never by the client.
    /// <para>
    /// "The 31st" in February is the 28th, or the 29th in a leap year. Two clients deciding this
    /// independently would show a member two answers.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("2026-02-01T00:00:00Z", "2026-02-28T08:00:00Z")]
    [InlineData("2028-02-01T00:00:00Z", "2028-02-29T08:00:00Z")]
    public void A_day_of_month_that_does_not_exist_is_clamped(string now, string expected)
    {
        var automation = Draft();
        automation.SetSchedule(
            AutomationFrequency.Monthly, AutomationTimezoneMode.UserSelected, "Africa/Lagos",
            9, 0, null, 31, null, null, null, null, null, null);

        AutomationScheduleCalculator.Next(automation, DateTimeOffset.Parse(now, CultureInfo.InvariantCulture))
            .NextRunAt.Should().Be(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture));
    }

    // ── The refusals ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A custom frequency produces nothing, and says so in words a member can act on.
    /// <para>
    /// It is held as they wrote it and never parsed. A member-authored recurrence the platform
    /// interpreted would be a rules language exposed to members with a scheduler behind it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_custom_frequency_is_never_parsed()
    {
        var automation = Draft();
        automation.SetSchedule(
            AutomationFrequency.Custom, AutomationTimezoneMode.UserSelected, "Africa/Lagos",
            9, 0, null, null, null, null, null, null, null, "every other Tuesday unless it rains");

        var plan = AutomationScheduleCalculator.Next(automation, DateTimeOffset.Parse("2026-01-15T00:00:00Z", CultureInfo.InvariantCulture));

        plan.NextRunAt.Should().BeNull();
        plan.Reason.Should().Contain("custom schedule");
    }

    /// <summary>A schedule past its end date produces nothing.</summary>
    [Fact]
    public void A_schedule_past_its_end_date_produces_nothing()
    {
        var automation = Draft();
        automation.SetSchedule(
            AutomationFrequency.Daily, AutomationTimezoneMode.UserSelected, "Africa/Lagos",
            9, 0, null, null, null, DateTimeOffset.Parse("2026-01-10T00:00:00Z", CultureInfo.InvariantCulture),
            null, null, null, null);

        AutomationScheduleCalculator.Next(automation, DateTimeOffset.Parse("2026-01-15T00:00:00Z", CultureInfo.InvariantCulture))
            .NextRunAt.Should().BeNull();
    }

    // ── The catch-up policy ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// An occurrence inside the catch-up window still fires; one past it does not.
    /// <para>
    /// A daily reminder that arrives eleven hours late is worse than one that does not arrive, and
    /// the member has to be able to see it was skipped and why.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(10, true)]
    [InlineData(59, true)]
    [InlineData(61, false)]
    [InlineData(600, false)]
    public void The_catch_up_window_decides_whether_a_late_occurrence_fires(
        int minutesLate, bool fires)
    {
        var due = DateTimeOffset.Parse("2026-01-15T08:00:00Z", CultureInfo.InvariantCulture);

        AutomationScheduleCalculator.IsWithinCatchUp(due, due.AddMinutes(minutesLate), 60)
            .Should().Be(fires);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static AutomationDefinition Daily(string zone, int hour, int minute)
    {
        var automation = Draft();

        automation.SetSchedule(
            AutomationFrequency.Daily, AutomationTimezoneMode.UserSelected, zone,
            hour, minute, null, null, null, null, null, null, null, null);

        return automation;
    }

    private static AutomationDefinition Draft() =>
        AutomationDefinition.Create(
            AutomationOwnerKind.User,
            Guid.NewGuid(),
            "You",
            UserId.New(),
            "Test",
            string.Empty,
            AutomationSource.Builder,
            null,
            "manual.run",
            "{}",
            activateImmediately: true,
            DateTimeOffset.UnixEpoch,
            null);
}
