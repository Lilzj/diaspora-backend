using System.Globalization;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;

namespace Diaspora.Modules.Automation.Application.Engine;

/// <summary>What the next occurrence is, and anything the member should be told about it.</summary>
/// <param name="NextRunAt">The absolute instant, or null when the schedule cannot produce one.</param>
/// <param name="LocalWallClock">The wall clock the member asked for, resolved.</param>
/// <param name="TimeZoneId">The zone it was resolved in.</param>
/// <param name="Reason">Why there is no next occurrence. Null when there is one.</param>
/// <param name="Warnings">
/// Things that are true and worth saying: a wall-clock time that does not exist on the chosen day
/// because the clocks went forward, or one that happens twice because they went back.
/// </param>
public readonly record struct AutomationOccurrencePlan(
    DateTimeOffset? NextRunAt,
    string LocalWallClock,
    string TimeZoneId,
    string? Reason,
    IReadOnlyList<string> Warnings)
{
    public static AutomationOccurrencePlan None(string reason, string timeZoneId = "UTC") =>
        new(null, string.Empty, timeZoneId, reason, []);
}

/// <summary>
/// Works out when a time-based automation is next due.
/// <para>
/// <b>The clock the member chose is a wall clock, and the platform stores an instant.</b> Converting
/// between the two is where every scheduling defect on every platform lives, and there are three
/// decisions here that decide whether it is correct:
/// </para>
/// <para>
/// <b>1. An IANA identifier, never an offset.</b> An offset is wrong for half the year in every zone
/// that observes daylight saving, and the failure is silent — a 09:00 daily reminder simply arrives at
/// 08:00 for six months. <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> resolves IANA identifiers
/// on every platform this deployment runs on, because .NET's ICU-backed lookup accepts them on Windows
/// as well as on Linux.
/// </para>
/// <para>
/// <b>2. A skipped wall-clock time moves forward, and a repeated one takes the first.</b> When the
/// clocks go forward, 01:30 does not exist on that day; when they go back, it happens twice. .NET's
/// own conversion throws on the first and silently picks one on the second, so both are handled
/// explicitly: a non-existent time is pushed past the gap, and an ambiguous one takes the earlier
/// offset. Both produce a warning the member sees, because a reminder that moved by an hour without
/// anybody saying so is exactly the surprise §42 exists to prevent.
/// </para>
/// <para>
/// <b>3. A day-of-month that does not exist is clamped, here and never on the client.</b> "The 31st"
/// in February is the 28th, or the 29th. The client is explicitly forbidden from deciding this,
/// because two clients would decide it differently and the member would see two answers.
/// </para>
/// <para>
/// <b>A custom frequency produces nothing.</b> It is a member's own words and is never parsed. A
/// custom recurrence the platform interpreted would be a rules language exposed to members with a
/// scheduler behind it.
/// </para>
/// </summary>
public static class AutomationScheduleCalculator
{
    /// <summary>
    /// The next occurrence strictly after <paramref name="after"/>.
    /// </summary>
    public static AutomationOccurrencePlan Next(AutomationDefinition automation, DateTimeOffset after)
    {
        ArgumentNullException.ThrowIfNull(automation);

        if (!automation.HasSchedule)
        {
            return AutomationOccurrencePlan.None("This automation is not on a schedule.");
        }

        if (automation.Frequency == AutomationFrequency.Custom)
        {
            return AutomationOccurrencePlan.None(
                "A custom schedule is held as you wrote it and is not something the platform can run. "
                + "Choose once, daily, weekly or monthly to have it run on its own.");
        }

        if (automation.Frequency == AutomationFrequency.EventRelative)
        {
            return AutomationOccurrencePlan.None(
                "This runs relative to something else, so its next time comes from that.");
        }

        if (!TryResolveZone(automation.TimeZoneId, out var zone))
        {
            return AutomationOccurrencePlan.None(
                $"'{automation.TimeZoneId}' is not a time zone this platform recognises.",
                automation.TimeZoneId ?? "UTC");
        }

        if (automation.EndsAt is { } endsAt && after >= endsAt)
        {
            return AutomationOccurrencePlan.None(
                "This automation has passed the end date you set.", zone.Id);
        }

        var hour = automation.Hour ?? 9;
        var minute = automation.Minute ?? 0;

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return AutomationOccurrencePlan.None("That time of day is not a valid one.", zone.Id);
        }

        // Everything below reasons in the member's own wall clock and converts once, at the end. The
        // opposite order — walking instants and formatting them — is what makes a daily schedule drift
        // by an hour across a daylight-saving boundary.
        var floor = automation.StartsAt is { } startsAt && startsAt > after ? startsAt : after;
        var localFloor = TimeZoneInfo.ConvertTime(floor, zone);
        var warnings = new List<string>();

        var candidate = automation.Frequency switch
        {
            AutomationFrequency.Once => OnceDate(automation, localFloor, hour, minute),
            AutomationFrequency.Daily => DailyDate(localFloor, hour, minute),
            AutomationFrequency.Weekly => WeeklyDate(automation, localFloor, hour, minute),
            AutomationFrequency.Monthly => MonthlyDate(automation, localFloor, hour, minute),
            _ => null
        };

        if (candidate is null)
        {
            return AutomationOccurrencePlan.None(
                "This schedule does not describe a time the platform can work out.", zone.Id);
        }

        var resolved = Resolve(candidate.Value, zone, warnings);

        if (automation.EndsAt is { } end && resolved > end)
        {
            return AutomationOccurrencePlan.None(
                "The next time would be after the end date you set.", zone.Id);
        }

        return new AutomationOccurrencePlan(
            resolved,
            candidate.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            zone.Id,
            null,
            warnings);
    }

    /// <summary>
    /// Whether an IANA identifier is one this deployment can resolve.
    /// <para>
    /// Checked on write rather than only at execution, because a schedule saved with an unknown zone
    /// is a schedule that will never fire and the member should be told at the moment they set it.
    /// </para>
    /// </summary>
    public static bool IsKnownTimeZone(string? id) => TryResolveZone(id, out _);

    /// <summary>Renders an instant in a zone, for a member-facing sentence.</summary>
    public static string Describe(DateTimeOffset instant, string? timeZoneId)
    {
        if (!TryResolveZone(timeZoneId, out var zone))
        {
            return instant.ToString("f", CultureInfo.InvariantCulture) + " UTC";
        }

        var local = TimeZoneInfo.ConvertTime(instant, zone);
        return $"{local.ToString("f", CultureInfo.InvariantCulture)} ({zone.Id})";
    }

    /// <summary>
    /// Whether a due moment is still worth firing.
    /// <para>
    /// The catch-up policy, and it is a server decision stated once rather than a guess two clients
    /// would make differently.
    /// </para>
    /// </summary>
    public static bool IsWithinCatchUp(DateTimeOffset dueAt, DateTimeOffset now, int catchUpMinutes) =>
        now - dueAt <= TimeSpan.FromMinutes(catchUpMinutes);

    private static bool TryResolveZone(string? id, out TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            zone = TimeZoneInfo.Utc;
            return true;
        }

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static DateTime? OnceDate(
        AutomationDefinition automation, DateTimeOffset localFloor, int hour, int minute)
    {
        // A one-off runs at its start date, or — where none was given — at the next occurrence of the
        // chosen wall clock. It never repeats, and the caller schedules exactly one occurrence.
        if (automation.StartsAt is { } startsAt)
        {
            return startsAt.UtcDateTime > localFloor.UtcDateTime
                ? new DateTime(
                    startsAt.Year, startsAt.Month, startsAt.Day, hour, minute, 0, DateTimeKind.Unspecified)
                : null;
        }

        return DailyDate(localFloor, hour, minute);
    }

    private static DateTime DailyDate(DateTimeOffset localFloor, int hour, int minute)
    {
        var today = new DateTime(
            localFloor.Year, localFloor.Month, localFloor.Day, hour, minute, 0, DateTimeKind.Unspecified);

        return today > localFloor.DateTime ? today : today.AddDays(1);
    }

    private static DateTime? WeeklyDate(
        AutomationDefinition automation, DateTimeOffset localFloor, int hour, int minute)
    {
        var days = automation.DayOfWeekList();

        if (days.Count == 0)
        {
            return null;
        }

        // Up to eight days rather than seven: a schedule for today at a time that has already passed
        // has to reach the same weekday next week, and a seven-day walk stops one short of it.
        for (var offset = 0; offset <= 7; offset++)
        {
            var day = localFloor.Date.AddDays(offset);

            if (!days.Contains((int)day.DayOfWeek))
            {
                continue;
            }

            var candidate = new DateTime(
                day.Year, day.Month, day.Day, hour, minute, 0, DateTimeKind.Unspecified);

            if (candidate > localFloor.DateTime)
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateTime? MonthlyDate(
        AutomationDefinition automation, DateTimeOffset localFloor, int hour, int minute)
    {
        var requested = automation.DayOfMonth ?? localFloor.Day;

        if (requested is < 1 or > 31)
        {
            return null;
        }

        // Two months rather than one, for the same reason the weekly walk takes eight days: a 31st
        // asked for on the 31st at a time that has passed has to reach the next month that has one.
        for (var offset = 0; offset <= 2; offset++)
        {
            var month = new DateTime(localFloor.Year, localFloor.Month, 1, 0, 0, 0, DateTimeKind.Unspecified)
                .AddMonths(offset);

            // Clamped here, and never on the client. "The 31st" in February is the 28th or the 29th,
            // and two clients deciding it independently would show a member two answers.
            var day = Math.Min(requested, DateTime.DaysInMonth(month.Year, month.Month));

            var candidate = new DateTime(
                month.Year, month.Month, day, hour, minute, 0, DateTimeKind.Unspecified);

            if (candidate > localFloor.DateTime)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Turns a wall clock into an instant, handling the two days a year it is not a function.
    /// </summary>
    private static DateTimeOffset Resolve(DateTime local, TimeZoneInfo zone, List<string> warnings)
    {
        if (zone.IsInvalidTime(local))
        {
            // The clocks went forward and this wall clock does not exist. Pushed past the gap rather
            // than refused: a member whose 01:30 daily reminder falls in the missing hour once a year
            // wants it at 02:30 that day, not silently skipped.
            var adjustment = zone.GetAdjustmentRules()
                .FirstOrDefault(rule => local.Date >= rule.DateStart && local.Date <= rule.DateEnd);

            var delta = adjustment?.DaylightDelta ?? TimeSpan.FromHours(1);
            var moved = local.Add(delta);

            warnings.Add(
                $"The clocks change that day and {local:HH:mm} does not exist, so this will run at "
                + $"{moved:HH:mm} instead.");

            return new DateTimeOffset(moved, zone.GetUtcOffset(moved));
        }

        if (zone.IsAmbiguousTime(local))
        {
            // The clocks went back and this wall clock happens twice. The earlier offset is taken —
            // stated, rather than left to whichever .NET picks — so two instances computing the same
            // schedule agree.
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var earliest = offsets.Max();

            warnings.Add(
                $"The clocks change that day and {local:HH:mm} happens twice. This will run the first "
                + "time.");

            return new DateTimeOffset(local, earliest);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
