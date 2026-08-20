using System.Globalization;

namespace Diaspora.Platform.Operations.Scheduling;

/// <summary>
/// A five-field cron expression: minute, hour, day-of-month, month, day-of-week.
/// <para>
/// <b>Written rather than taken from a package, and that needs a justification.</b> The platform's
/// rule is provider independence, and a scheduling library brings its own storage, its own dashboard
/// and its own opinion about how a job is defined — all of which this phase has already decided. What
/// was actually needed was "given an expression and a moment, when next?", which is this file.
/// </para>
/// <para>
/// <b>What it supports:</b> <c>*</c>, a number, <c>a-b</c>, <c>a,b,c</c>, <c>*&#47;n</c> and
/// <c>a-b&#47;n</c> in every field; three-letter month and day names; and the two standard aliases
/// <c>@hourly</c> and <c>@daily</c> alongside the rest.
/// </para>
/// <para>
/// <b>What it does not, said plainly rather than discovered:</b> no seconds field, no <c>L</c>,
/// <c>W</c>, <c>#</c> or <c>?</c>, and no year. A parse of any of those throws at registration —
/// which is at startup, where a bad schedule should be found — rather than silently never firing.
/// </para>
/// <para>
/// Times are UTC. A recurring job on a wall clock that shifts twice a year either runs twice or not
/// at all on those two days, and no cron field can express which was intended.
/// </para>
/// </summary>
public sealed class CronExpression
{
    private const int MaxSearchDays = 400;

    private readonly bool[] _minutes = new bool[60];
    private readonly bool[] _hours = new bool[24];
    private readonly bool[] _daysOfMonth = new bool[32];
    private readonly bool[] _months = new bool[13];
    private readonly bool[] _daysOfWeek = new bool[7];
    private readonly bool _dayOfMonthRestricted;
    private readonly bool _dayOfWeekRestricted;

    private CronExpression(string expression, string[] fields)
    {
        Expression = expression;

        _dayOfMonthRestricted = fields[2] != "*";
        _dayOfWeekRestricted = fields[4] != "*";

        Fill(_minutes, fields[0], 0, 59, null);
        Fill(_hours, fields[1], 0, 23, null);
        Fill(_daysOfMonth, fields[2], 1, 31, null);
        Fill(_months, fields[3], 1, 12, MonthNames);
        Fill(_daysOfWeek, fields[4], 0, 6, DayNames);
    }

    /// <summary>The expression as written, for diagnostics and the dashboard.</summary>
    public string Expression { get; }

    public static CronExpression Parse(string expression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);

        var normalized = expression.Trim().ToLowerInvariant() switch
        {
            "@hourly" => "0 * * * *",
            "@daily" or "@midnight" => "0 0 * * *",
            "@weekly" => "0 0 * * 0",
            "@monthly" => "0 0 1 * *",
            "@yearly" or "@annually" => "0 0 1 1 *",
            var other => other
        };

        var fields = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (fields.Length != 5)
        {
            throw new FormatException(
                $"'{expression}' is not a five-field cron expression (minute hour day-of-month month " +
                $"day-of-week). It has {fields.Length.ToString(CultureInfo.InvariantCulture)} field(s).");
        }

        return new CronExpression(expression, fields);
    }

    public static bool TryParse(string expression, out CronExpression? parsed)
    {
        try
        {
            parsed = Parse(expression);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            parsed = null;
            return false;
        }
    }

    /// <summary>
    /// The first moment strictly after <paramref name="after"/> that matches, or null if none falls
    /// within the search horizon.
    /// <para>
    /// Strictly after, so calling this with a job's last run never returns that same run. A bounded
    /// horizon rather than an unbounded loop, because an expression like <c>0 0 30 2 *</c> — the
    /// thirtieth of February — matches nothing, ever, and a scheduler that searched forever for it
    /// would hang a worker rather than report a bad schedule.
    /// </para>
    /// </summary>
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after)
    {
        var candidate = new DateTimeOffset(
            after.UtcDateTime.Year, after.UtcDateTime.Month, after.UtcDateTime.Day,
            after.UtcDateTime.Hour, after.UtcDateTime.Minute, 0, TimeSpan.Zero)
            .AddMinutes(1);

        var horizon = candidate.AddDays(MaxSearchDays);

        while (candidate < horizon)
        {
            if (!_months[candidate.Month])
            {
                // Skip to the first minute of the next month rather than trying every minute of a
                // month that cannot match. Without this, a yearly expression walks half a million
                // candidates.
                candidate = new DateTimeOffset(candidate.Year, candidate.Month, 1, 0, 0, 0, TimeSpan.Zero)
                    .AddMonths(1);
                continue;
            }

            if (!MatchesDay(candidate))
            {
                candidate = candidate.Date.AddDays(1);
                candidate = new DateTimeOffset(candidate.DateTime, TimeSpan.Zero);
                continue;
            }

            if (!_hours[candidate.Hour])
            {
                candidate = candidate.AddHours(1).AddMinutes(-candidate.Minute);
                continue;
            }

            if (_minutes[candidate.Minute])
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    /// <summary>
    /// Cron's day rule, which is the one thing about the format that surprises everybody.
    /// <para>
    /// When both the day-of-month and the day-of-week fields are restricted, a moment matches if it
    /// satisfies <em>either</em> — not both. <c>0 0 1 * 1</c> means "the first of the month, and also
    /// every Monday", which is what the original crontab did and what every implementation has copied
    /// since. Getting this backwards makes such expressions fire almost never instead of often.
    /// </para>
    /// </summary>
    private bool MatchesDay(DateTimeOffset candidate)
    {
        var dayOfMonth = _daysOfMonth[candidate.Day];
        var dayOfWeek = _daysOfWeek[(int)candidate.DayOfWeek];

        if (_dayOfMonthRestricted && _dayOfWeekRestricted)
        {
            return dayOfMonth || dayOfWeek;
        }

        return dayOfMonth && dayOfWeek;
    }

    private static void Fill(bool[] target, string field, int min, int max, string[]? names)
    {
        foreach (var part in field.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var step = 1;
            var body = part;

            var slash = part.IndexOf('/', StringComparison.Ordinal);

            if (slash >= 0)
            {
                body = part[..slash];

                if (!int.TryParse(part[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out step) ||
                    step < 1)
                {
                    throw new FormatException($"'{part}' has an invalid step.");
                }
            }

            int from;
            int to;

            if (body is "*")
            {
                from = min;
                to = max;
            }
            else if (body.Contains('-', StringComparison.Ordinal))
            {
                var bounds = body.Split('-', 2);
                from = Value(bounds[0], min, max, names);
                to = Value(bounds[1], min, max, names);

                if (to < from)
                {
                    throw new FormatException($"'{body}' is a descending range.");
                }
            }
            else
            {
                from = Value(body, min, max, names);
                to = slash >= 0 ? max : from;
            }

            for (var value = from; value <= to; value += step)
            {
                // Sunday is both 0 and 7 in every cron dialect. Normalising here rather than widening
                // the array keeps the day-of-week lookup a direct index on DayOfWeek.
                target[value == 7 && max == 6 ? 0 : value] = true;
            }
        }
    }

    private static int Value(string token, int min, int max, string[]? names)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            if (names is null)
            {
                throw new FormatException($"'{token}' is not a number.");
            }

            var index = Array.FindIndex(
                names, name => string.Equals(name, token, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                throw new FormatException($"'{token}' is not a recognised name.");
            }

            value = index + min;
        }

        // 7 for Sunday is legal input in a 0–6 field; everything else must be inside the bounds.
        if (value == 7 && max == 6)
        {
            return 7;
        }

        if (value < min || value > max)
        {
            throw new FormatException(
                $"'{token}' is outside the permitted range " +
                $"{min.ToString(CultureInfo.InvariantCulture)}–{max.ToString(CultureInfo.InvariantCulture)}.");
        }

        return value;
    }

    private static readonly string[] MonthNames =
        ["jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec"];

    private static readonly string[] DayNames =
        ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    public override string ToString() => Expression;
}
