using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// Turns a rule into dates.
/// <para>
/// <b>Pure, static and in the domain, which is the point.</b> Expansion is the one piece of this
/// feature that is neither a database question nor an HTTP question: given a rule, a local time and
/// a zone, the dates a series falls on are a fact, and a fact worth being able to test without a
/// host, a context or a clock. Every occurrence the module ever writes comes from here, so there is
/// exactly one place where "what does every other Tuesday mean" is answered.
/// </para>
/// <para>
/// <b>Sequence numbers count generated dates, not surviving ones.</b> An excluded date consumes its
/// ordinal and is then dropped, which is RFC 5545's own reading of <c>COUNT</c> against
/// <c>EXDATE</c> — and, more usefully here, it is what keeps a sequence number stable when an
/// organizer cancels one week. A numbering that closed the gap would renumber every later
/// occurrence, and every stored reference to one would move.
/// </para>
/// <para>
/// <b>Short months are skipped rather than clamped.</b> "The 31st of every month" produces no
/// February date, exactly as the standard says; clamping it to the 28th would put a monthly meeting
/// three days early once a year without anybody asking for it. The same applies to the 29th of
/// February on a yearly rule.
/// </para>
/// <para>
/// <b>Every loop is bounded.</b> A rule can be legal and generate nothing — the 31st, monthly,
/// every twelve months, starting in February — so the walk stops after
/// <see cref="MaxPeriodsExamined"/> barren periods rather than spinning.
/// </para>
/// </summary>
public static class RecurrenceExpander
{
    /// <summary>
    /// How many periods the walk will examine before giving up on a rule that is producing nothing.
    /// <para>
    /// Four hundred covers a daily rule across a year, a weekly rule across seven, and a monthly one
    /// across thirty-three — comfortably past any horizon this module materialises to.
    /// </para>
    /// </summary>
    public const int MaxPeriodsExamined = 400;

    /// <summary>
    /// The slots a rule produces, in order.
    /// </summary>
    /// <param name="rule">The recurrence.</param>
    /// <param name="schedule">The local time of day, duration and zone that resolve a date.</param>
    /// <param name="exceptions">
    /// Local dates the rule generates and that were removed — a cancelled occurrence, or one moved
    /// off its generated date. Excluded from the result, but they still consume their ordinal.
    /// </param>
    /// <param name="fromSequence">The first ordinal to return. 1 on a first expansion.</param>
    /// <param name="maxSlots">How many to return. Bounds the caller's write.</param>
    /// <param name="notAfterLocalDate">
    /// The materialisation horizon. Null means "as far as the rule itself allows", which is only
    /// finite for a rule that ends.
    /// </param>
    public static IReadOnlyList<RecurrenceSlot> Expand(
        RecurrenceRule rule,
        SeriesSchedule schedule,
        IReadOnlySet<DateOnly>? exceptions = null,
        int fromSequence = 1,
        int maxSlots = 100,
        DateOnly? notAfterLocalDate = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(schedule);

        if (maxSlots < 1)
        {
            return [];
        }

        var slots = new List<RecurrenceSlot>();
        var sequence = 0;

        foreach (var date in Dates(rule, schedule.FirstLocalDate))
        {
            sequence++;

            if (rule.TotalOccurrences is { } total && sequence > total)
            {
                break;
            }

            if (rule.EndKind == RecurrenceEndKind.OnDate
                && rule.Until is { } until
                && date > until)
            {
                break;
            }

            if (notAfterLocalDate is { } horizon && date > horizon)
            {
                break;
            }

            if (sequence < fromSequence || exceptions?.Contains(date) == true)
            {
                continue;
            }

            slots.Add(new RecurrenceSlot(
                sequence, date, schedule.ToStart(date), schedule.ToEnd(date)));

            if (slots.Count >= maxSlots)
            {
                break;
            }
        }

        return slots;
    }

    /// <summary>
    /// The ordinal a given local date holds in the rule, or null when the rule never generates it.
    /// <para>
    /// The inverse of <see cref="Expand"/>, and needed by exactly one caller: an occurrence moved
    /// off its generated date has to record which date it came from, and a later "this and future"
    /// edit has to know where that sits in the order.
    /// </para>
    /// </summary>
    public static int? SequenceOf(
        RecurrenceRule rule, SeriesSchedule schedule, DateOnly localDate)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(schedule);

        var sequence = 0;

        foreach (var date in Dates(rule, schedule.FirstLocalDate))
        {
            sequence++;

            if (date == localDate)
            {
                return sequence;
            }

            if (date > localDate)
            {
                return null;
            }

            if (rule.TotalOccurrences is { } total && sequence >= total)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The rule's dates, lazily, ignoring exceptions and every end condition but the walk bound.
    /// The caller applies <c>COUNT</c>, <c>UNTIL</c> and the horizon, so all three read in one place.
    /// </summary>
    private static IEnumerable<DateOnly> Dates(RecurrenceRule rule, DateOnly firstDate) =>
        rule.Frequency switch
        {
            RecurrenceFrequency.Daily => Daily(rule, firstDate),
            RecurrenceFrequency.Weekly => Weekly(rule, firstDate),
            RecurrenceFrequency.Monthly => Monthly(rule, firstDate),
            RecurrenceFrequency.Yearly => Yearly(rule, firstDate),
            _ => Daily(rule, firstDate)
        };

    private static IEnumerable<DateOnly> Daily(RecurrenceRule rule, DateOnly firstDate)
    {
        for (var period = 0; period < MaxPeriodsExamined; period++)
        {
            yield return firstDate.AddDays(period * rule.Interval);
        }
    }

    /// <summary>
    /// Week by week, stepping by the interval, emitting the named weekdays within each.
    /// <para>
    /// <b>Weeks start on Monday</b> — the standard's default <c>WKST</c>, and the convention every
    /// place this platform operates in shares. It only matters for an interval above one, where it
    /// decides which weeks are "every other"; a Sunday-start convention would shift the whole
    /// pattern by a week for a fortnightly Sunday service.
    /// </para>
    /// <para>
    /// Dates before the start are skipped rather than emitted: a Monday-and-Thursday rule beginning
    /// on the Wednesday starts on the Thursday, not on the Monday that already passed.
    /// </para>
    /// </summary>
    private static IEnumerable<DateOnly> Weekly(RecurrenceRule rule, DateOnly firstDate)
    {
        var weekdays = rule.Weekdays.Count > 0
            ? rule.Weekdays.OrderBy(IsoDay).ToList()
            : [firstDate.DayOfWeek];

        var weekStart = firstDate.AddDays(-(IsoDay(firstDate.DayOfWeek) - 1));

        for (var period = 0; period < MaxPeriodsExamined; period++)
        {
            var start = weekStart.AddDays(period * rule.Interval * 7);

            foreach (var day in weekdays)
            {
                var date = start.AddDays(IsoDay(day) - 1);

                if (date >= firstDate)
                {
                    yield return date;
                }
            }
        }
    }

    private static IEnumerable<DateOnly> Monthly(RecurrenceRule rule, DateOnly firstDate)
    {
        var monthDays = rule.MonthDays.Count > 0 ? rule.MonthDays : [firstDate.Day];
        var anchor = new DateOnly(firstDate.Year, firstDate.Month, 1);

        for (var period = 0; period < MaxPeriodsExamined; period++)
        {
            var month = anchor.AddMonths(period * rule.Interval);
            var lengthOfMonth = DateTime.DaysInMonth(month.Year, month.Month);

            var resolved = monthDays
                .Select(day => day > 0 ? day : lengthOfMonth + day + 1)
                .Where(day => day >= 1 && day <= lengthOfMonth)
                .Distinct()
                .OrderBy(day => day);

            foreach (var day in resolved)
            {
                var date = new DateOnly(month.Year, month.Month, day);

                if (date >= firstDate)
                {
                    yield return date;
                }
            }
        }
    }

    private static IEnumerable<DateOnly> Yearly(RecurrenceRule rule, DateOnly firstDate)
    {
        for (var period = 0; period < MaxPeriodsExamined; period++)
        {
            var year = firstDate.Year + (period * rule.Interval);

            // 29 February in a common year. Skipped rather than moved: an anniversary on the 29th
            // is on the 29th, and a platform that silently moved it to the 28th would be wrong for
            // three years out of four in a way nobody would notice until it mattered.
            if (firstDate.Month == 2 && firstDate.Day == 29 && !DateTime.IsLeapYear(year))
            {
                continue;
            }

            yield return new DateOnly(year, firstDate.Month, firstDate.Day);
        }
    }

    /// <summary>
    /// <see cref="DayOfWeek"/> numbers Sunday as zero; ISO numbers it as seven. Every comparison in
    /// this file goes through here, because the off-by-one between the two is the likeliest bug in
    /// any recurrence implementation and it should exist in exactly one expression.
    /// </summary>
    private static int IsoDay(DayOfWeek day) => day == DayOfWeek.Sunday ? 7 : (int)day;
}
