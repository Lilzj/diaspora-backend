using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.CommunityLife;

/// <summary>
/// The dates a rule produces.
/// <para>
/// <b>The single highest-value test file in B12.1.</b> Every occurrence the platform ever writes
/// comes out of the expander, and every one of its failure modes is silent: a rule that produces
/// nothing looks like a horizon that has not caught up, a rule that skips a week looks like a
/// cancellation, and a rule that drifts an hour across a daylight-saving boundary looks like an
/// organizer who typed the wrong time. None of them raises anything.
/// </para>
/// </summary>
public class RecurrenceExpanderTests
{
    private static readonly DateOnly Monday = new(2026, 8, 3);

    /// <summary>Every N days from the start, and the start counts as the first.</summary>
    [Fact]
    public void A_daily_rule_steps_by_its_interval_and_includes_the_start()
    {
        var slots = Expand(Rule(RecurrenceFrequency.Daily, interval: 3), maxSlots: 3);

        slots.Select(slot => slot.LocalDate).Should().Equal(
            Monday, Monday.AddDays(3), Monday.AddDays(6));
    }

    /// <summary>An empty weekday list means "the weekday the series starts on".</summary>
    [Fact]
    public void A_weekly_rule_without_weekdays_repeats_on_the_start_weekday()
    {
        var slots = Expand(Rule(RecurrenceFrequency.Weekly), maxSlots: 3);

        slots.Select(slot => slot.LocalDate.DayOfWeek).Should().AllBeEquivalentTo(DayOfWeek.Monday);
        slots.Select(slot => slot.LocalDate).Should().Equal(
            Monday, Monday.AddDays(7), Monday.AddDays(14));
    }

    /// <summary>
    /// A Monday-and-Thursday rule beginning on the Wednesday starts on the Thursday.
    /// <para>
    /// The generator walks whole weeks, so without the guard it would emit the Monday that already
    /// passed — putting an occurrence before the series began.
    /// </para>
    /// </summary>
    [Fact]
    public void A_weekly_rule_never_emits_a_date_before_the_series_starts()
    {
        var wednesday = new DateOnly(2026, 8, 5);

        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly, byWeekday: [1, 4]),
            Schedule(wednesday),
            maxSlots: 3);

        slots[0].LocalDate.Should().Be(new DateOnly(2026, 8, 6), "the Thursday of that week");
        slots[1].LocalDate.Should().Be(new DateOnly(2026, 8, 10), "then the following Monday");
    }

    /// <summary>An interval above one skips whole weeks, not individual days.</summary>
    [Fact]
    public void A_fortnightly_rule_skips_the_week_between()
    {
        var slots = Expand(Rule(RecurrenceFrequency.Weekly, interval: 2), maxSlots: 3);

        slots.Select(slot => slot.LocalDate).Should().Equal(
            Monday, Monday.AddDays(14), Monday.AddDays(28));
    }

    /// <summary>
    /// A month that does not have the named day is skipped, not clamped.
    /// <para>
    /// "The 31st of every month" produces no February date. Clamping to the 28th would move a
    /// monthly meeting three days early once a year without anybody asking.
    /// </para>
    /// </summary>
    [Fact]
    public void A_monthly_rule_skips_a_month_that_lacks_the_day()
    {
        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Monthly, byMonthDay: [31]),
            Schedule(new DateOnly(2027, 1, 31)),
            maxSlots: 3);

        slots.Select(slot => slot.LocalDate.Month).Should().NotContain(
            2, "February has no 31st");

        slots[0].LocalDate.Should().Be(new DateOnly(2027, 1, 31));
        slots[1].LocalDate.Should().Be(new DateOnly(2027, 3, 31));
    }

    /// <summary>A negative day counts back from the end of the month.</summary>
    [Fact]
    public void A_negative_month_day_counts_back_from_the_end()
    {
        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Monthly, byMonthDay: [-1]),
            Schedule(new DateOnly(2027, 1, 1)),
            maxSlots: 2);

        slots[0].LocalDate.Should().Be(new DateOnly(2027, 1, 31));
        slots[1].LocalDate.Should().Be(new DateOnly(2027, 2, 28), "2027 is not a leap year");
    }

    /// <summary>29 February is skipped in a common year rather than moved to the 28th.</summary>
    [Fact]
    public void A_yearly_rule_on_the_twenty_ninth_of_february_skips_common_years()
    {
        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Yearly),
            Schedule(new DateOnly(2028, 2, 29)),
            maxSlots: 2);

        slots[0].LocalDate.Should().Be(new DateOnly(2028, 2, 29));
        slots[1].LocalDate.Should().Be(new DateOnly(2032, 2, 29), "the next leap year");
    }

    // ── Ending ──────────────────────────────────────────────────────────────────────────────

    /// <summary><c>AfterCount</c> counts the first occurrence.</summary>
    [Fact]
    public void A_counted_rule_stops_after_its_count()
    {
        var rule = Rule(
            RecurrenceFrequency.Weekly, endKind: RecurrenceEndKind.AfterCount, count: 4);

        Expand(rule, maxSlots: 50).Should().HaveCount(4);
    }

    /// <summary>An until date is inclusive of an occurrence starting on it.</summary>
    [Fact]
    public void An_until_date_includes_an_occurrence_starting_on_it()
    {
        var rule = Rule(
            RecurrenceFrequency.Weekly,
            endKind: RecurrenceEndKind.OnDate,
            until: Monday.AddDays(14));

        Expand(rule, maxSlots: 50).Should().HaveCount(3);
    }

    // ── Exceptions and sequence numbering ───────────────────────────────────────────────────

    /// <summary>
    /// <b>An excluded date consumes its ordinal and is then dropped.</b> A numbering that closed the
    /// gap would renumber every later occurrence, and every stored reference to one would move.
    /// </summary>
    [Fact]
    public void An_exception_removes_its_date_without_renumbering_the_rest()
    {
        var excluded = Monday.AddDays(7);

        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly),
            Schedule(Monday),
            new HashSet<DateOnly> { excluded },
            maxSlots: 3);

        slots.Select(slot => slot.LocalDate).Should().NotContain(excluded);

        // 1, 3, 4 — the second ordinal was consumed by the excluded date and not reused. Closing
        // the gap would renumber every later occurrence and move every stored reference to one.
        slots.Select(slot => slot.Sequence).Should().Equal([1, 3, 4]);
    }

    /// <summary>Resuming from an ordinal skips everything before it and renumbers nothing.</summary>
    [Fact]
    public void Resuming_from_a_sequence_starts_there()
    {
        var slots = Expand(Rule(RecurrenceFrequency.Weekly), maxSlots: 2, fromSequence: 3);

        slots[0].Sequence.Should().Be(3);
        slots[0].LocalDate.Should().Be(Monday.AddDays(14));
    }

    /// <summary>
    /// A legal rule that can never fall on a real date terminates rather than spinning.
    /// <para>
    /// The 31st, every twelve months, starting in a February. The walk is bounded, so this returns
    /// empty — which the series aggregate then refuses at creation.
    /// </para>
    /// </summary>
    [Fact]
    public void A_rule_that_can_never_produce_a_date_terminates_empty()
    {
        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Monthly, interval: 12, byMonthDay: [31]),
            Schedule(new DateOnly(2027, 2, 1)),
            maxSlots: 5);

        slots.Should().BeEmpty();
    }

    /// <summary>The horizon stops the walk, and stops it on a local date.</summary>
    [Fact]
    public void The_horizon_bounds_the_walk()
    {
        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly),
            Schedule(Monday),
            exceptions: null,
            fromSequence: 1,
            maxSlots: 100,
            notAfterLocalDate: Monday.AddDays(20));

        slots.Should().HaveCount(3);
    }

    // ── Timezone correctness ────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>A weekly evening meeting stays at the same local time across a daylight-saving
    /// boundary.</b> This is the defect the whole schedule design exists to prevent: a generator that
    /// added 168 hours to the first occurrence would move a 19:00 London meeting to 18:00 every
    /// spring, and nobody would find out until a room was empty.
    /// </summary>
    [Fact]
    public void A_weekly_evening_keeps_its_local_time_across_the_spring_transition()
    {
        // The UK clocks go forward on 29 March 2026. A rule beginning a fortnight before it crosses.
        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 3, 15), new TimeOnly(19, 0), 120, "Europe/London").Value;

        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly), schedule, maxSlots: 4);

        slots.Should().HaveCountGreaterThan(3);

        foreach (var slot in slots)
        {
            var local = TimeZoneInfo.ConvertTime(
                slot.StartsAt, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));

            local.Hour.Should().Be(19, "the meeting is at seven in the evening in London");
        }

        // And the UTC instants genuinely differ, which is the proof the zone was applied rather
        // than ignored.
        slots[0].StartsAt.UtcDateTime.Hour.Should().Be(19);
        slots[^1].StartsAt.UtcDateTime.Hour.Should().Be(18);
    }

    /// <summary>
    /// A local time inside the hour the clocks skip is moved forward, not thrown away.
    /// <para>
    /// <c>TimeZoneInfo.ConvertTimeToUtc</c> throws on one, which inside a generator would abandon a
    /// whole series' expansion because of one morning in March.
    /// </para>
    /// </summary>
    [Fact]
    public void A_local_time_inside_the_skipped_hour_moves_forward()
    {
        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 3, 29), new TimeOnly(1, 30), 60, "Europe/London").Value;

        var start = schedule.ToStart(new DateOnly(2026, 3, 29));

        var local = TimeZoneInfo.ConvertTime(
            start, TimeZoneInfo.FindSystemTimeZoneById("Europe/London"));

        local.Hour.Should().Be(2, "01:30 does not exist that morning, so it becomes 02:30");
        local.Minute.Should().Be(30);
    }

    /// <summary>
    /// A local time that happens twice takes the earlier instant.
    /// <para>
    /// The daylight offset, which keeps the occurrence the same distance from the one before it.
    /// The framework's default is standard time, which would silently make that one occurrence an
    /// hour later than every other in its series.
    /// </para>
    /// </summary>
    [Fact]
    public void A_local_time_inside_the_repeated_hour_takes_the_earlier_instant()
    {
        // The UK clocks go back on 25 October 2026; 01:30 happens twice.
        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 10, 25), new TimeOnly(1, 30), 60, "Europe/London").Value;

        var start = schedule.ToStart(new DateOnly(2026, 10, 25));

        // 01:30 BST is 00:30 UTC; 01:30 GMT is 01:30 UTC. The earlier instant is the daylight one.
        //
        // This used to assert `start.Offset == +1h`, which passed and pinned a value that could
        // not be written: Npgsql refuses a non-zero offset on `timestamp with time zone`, so every
        // series outside UTC threw on save. The question was always which instant, not which
        // offset it was spelled with.
        start.UtcDateTime.Should().Be(
            new DateTime(2026, 10, 25, 0, 30, 0, DateTimeKind.Utc),
            "the earlier of the two instants that local time names");

        start.Offset.Should().Be(TimeSpan.Zero, "an instant is stored in UTC");
    }

    /// <summary>A zone with no daylight saving keeps one local time, which is most of the diaspora.</summary>
    [Fact]
    public void A_zone_without_daylight_saving_keeps_one_local_time()
    {
        var lagos = TimeZoneInfo.FindSystemTimeZoneById("Africa/Lagos");

        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 3, 15), new TimeOnly(19, 0), 120, "Africa/Lagos").Value;

        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly), schedule, maxSlots: 4);

        // Read back in the series' own zone. This used to assert `.Offset == +1h` on every slot,
        // which pinned a representation the database cannot store — see `SeriesSchedule.ToStart`.
        // Nineteen hundred in Lagos, every week, is what the organiser actually said.
        slots
            .Select(slot => TimeZoneInfo.ConvertTime(slot.StartsAt, lagos).TimeOfDay)
            .Should()
            .AllBeEquivalentTo(new TimeOnly(19, 0).ToTimeSpan());

        slots.Select(slot => slot.StartsAt.Offset).Should().AllBeEquivalentTo(TimeSpan.Zero);
    }

    /// <summary>The end instant is the start plus the duration, so an event may cross midnight.</summary>
    [Fact]
    public void An_occurrence_may_cross_midnight()
    {
        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 8, 3), new TimeOnly(23, 0), 180, "Europe/London").Value;

        var slot = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly), schedule, maxSlots: 1)[0];

        slot.EndsAt.Should().Be(slot.StartsAt.AddHours(3));
        slot.EndsAt.Should().BeAfter(slot.StartsAt);
    }

    // ── The inverse ─────────────────────────────────────────────────────────────────────────

    /// <summary>A date the rule generates reports its ordinal; one it does not reports nothing.</summary>
    [Fact]
    public void The_sequence_of_a_generated_date_is_its_ordinal()
    {
        var rule = Rule(RecurrenceFrequency.Weekly);
        var schedule = Schedule(Monday);

        RecurrenceExpander.SequenceOf(rule, schedule, Monday.AddDays(14)).Should().Be(3);
        RecurrenceExpander.SequenceOf(rule, schedule, Monday.AddDays(3)).Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every instant an expansion produces can actually be written.
    /// <para>
    /// Npgsql refuses a <c>DateTimeOffset</c> whose offset is not zero on
    /// <c>timestamp with time zone</c>, so a slot carrying its zone's own offset threw at
    /// <c>SaveChangesAsync</c> — not at validation, and not anywhere a test was looking. Creating a
    /// series in Port Louis failed with "Not created" and no more; so did Lagos, and so did London
    /// between March and October.
    /// </para>
    /// <para>
    /// It survived a full suite because the shared <c>Schedule</c> helper below uses <c>UTC</c>,
    /// the single zone for which the bug cannot fire, and the two tests that did use a real zone
    /// asserted on <c>.Offset</c> — agreeing with it. The zones here are deliberately ones the
    /// diaspora actually lives in, either side of UTC and with and without daylight saving.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Indian/Mauritius")]
    [InlineData("Africa/Lagos")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Dubai")]
    [InlineData("UTC")]
    public void Every_expanded_instant_is_storable(string timeZoneId)
    {
        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 6, 4), new TimeOnly(18, 0), 120, timeZoneId).Value;

        var slots = RecurrenceExpander.Expand(
            Rule(RecurrenceFrequency.Weekly), schedule, maxSlots: 12);

        slots.Should().NotBeEmpty();

        slots.Select(slot => slot.StartsAt.Offset).Should().AllBeEquivalentTo(TimeSpan.Zero);
        slots.Select(slot => slot.EndsAt.Offset).Should().AllBeEquivalentTo(TimeSpan.Zero);
    }

    /// <summary>Normalising the offset must not move the occurrence.</summary>
    [Fact]
    public void Storing_in_utc_keeps_the_local_time_the_organiser_chose()
    {
        var mauritius = TimeZoneInfo.FindSystemTimeZoneById("Indian/Mauritius");

        var schedule = SeriesSchedule.Create(
            new DateOnly(2026, 9, 17), new TimeOnly(18, 0), 120, "Indian/Mauritius").Value;

        var start = schedule.ToStart(new DateOnly(2026, 9, 17));

        // Eighteen hundred in Port Louis is fourteen hundred UTC. Both are the same instant, and
        // only one of them can be written.
        start.UtcDateTime.Should().Be(new DateTime(2026, 9, 17, 14, 0, 0, DateTimeKind.Utc));
        TimeZoneInfo.ConvertTime(start, mauritius).TimeOfDay.Should().Be(
            new TimeOnly(18, 0).ToTimeSpan());
    }

    private static IReadOnlyList<RecurrenceSlot> Expand(
        RecurrenceRule rule, int maxSlots, int fromSequence = 1) =>
        RecurrenceExpander.Expand(
            rule, Schedule(Monday), exceptions: null, fromSequence, maxSlots);

    private static SeriesSchedule Schedule(DateOnly firstDate) =>
        SeriesSchedule.Create(firstDate, new TimeOnly(19, 0), 120, "UTC").Value;

    private static RecurrenceRule Rule(
        RecurrenceFrequency frequency,
        int interval = 1,
        IReadOnlyList<int>? byWeekday = null,
        IReadOnlyList<int>? byMonthDay = null,
        RecurrenceEndKind endKind = RecurrenceEndKind.Never,
        DateOnly? until = null,
        int? count = null) =>
        RecurrenceRule.Create(
            frequency, interval, byWeekday, byMonthDay, endKind, until, count).Value;
}
