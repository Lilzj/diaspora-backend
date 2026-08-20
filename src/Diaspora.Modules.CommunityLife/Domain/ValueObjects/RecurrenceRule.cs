using System.Globalization;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// When a series repeats, as data.
/// <para>
/// <b>Shaped after RFC 5545's field names so the contract is a standard rather than an
/// invention</b> — <c>frequency</c>, <c>interval</c>, <c>byWeekday</c>, <c>byMonthDay</c>,
/// <c>until</c>, <c>count</c> — and deliberately a strict subset of it. See
/// <see cref="RecurrenceFrequency"/> for what was left out and why.
/// </para>
/// <para>
/// <b>The day lists are stored as canonical comma-separated text, not as owned collections.</b> Two
/// more tables to join on every read of a rule that is at most seven short numbers would be a poor
/// trade, and the canonical form — sorted, de-duplicated, invariant-culture — is what makes rule
/// equality work, which is what stops a no-op edit regenerating a hundred occurrences.
/// </para>
/// <para>
/// <b>There are no times here and no zone here.</b> A rule says which <em>dates</em> recur; the
/// local time of day, the duration and the IANA zone live on <see cref="SeriesSchedule"/>, because
/// they are what turns a date into an instant and getting that split wrong is how a weekly 19:00
/// meeting drifts to 18:00 every spring.
/// </para>
/// </summary>
public sealed class RecurrenceRule : ValueObject
{
    private const char Separator = ',';

    private RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        string byWeekday,
        string byMonthDay,
        RecurrenceEndKind endKind,
        DateOnly? until,
        int? count)
    {
        Frequency = frequency;
        Interval = interval;
        ByWeekday = byWeekday;
        ByMonthDay = byMonthDay;
        EndKind = endKind;
        Until = until;
        Count = count;
    }

    private RecurrenceRule() { }

    public RecurrenceFrequency Frequency { get; private set; }

    /// <summary>Every N periods. One means every week; two means every other week.</summary>
    public int Interval { get; private set; } = 1;

    /// <summary>
    /// ISO weekday numbers, sorted and comma-separated — <c>"1,3"</c> is Monday and Wednesday.
    /// Empty means "whichever weekday the series starts on".
    /// </summary>
    public string ByWeekday { get; private set; } = string.Empty;

    /// <summary>
    /// Days of the month, sorted and comma-separated. Negative counts back from the end, so
    /// <c>"-1"</c> is the last day. Empty means "whichever day of the month the series starts on".
    /// </summary>
    public string ByMonthDay { get; private set; } = string.Empty;

    public RecurrenceEndKind EndKind { get; private set; }

    /// <summary>The last date an occurrence may start on. Set only when <see cref="EndKind"/> is OnDate.</summary>
    public DateOnly? Until { get; private set; }

    /// <summary>How many occurrences in total. Set only when <see cref="EndKind"/> is AfterCount.</summary>
    public int? Count { get; private set; }

    public IReadOnlyList<DayOfWeek> Weekdays => ParseWeekdays(ByWeekday);

    public IReadOnlyList<int> MonthDays => ParseNumbers(ByMonthDay);

    /// <summary>
    /// Builds a rule, normalising everything a client can get slightly wrong.
    /// <para>
    /// <c>Custom</c> arrives here as <see cref="RecurrenceFrequency.Weekly"/> — the mapping layer
    /// does it, because a domain type should not know the wire had a fifth word for a frequency it
    /// does not have.
    /// </para>
    /// </summary>
    public static Result<RecurrenceRule> Create(
        RecurrenceFrequency frequency,
        int interval,
        IReadOnlyList<int>? byWeekday = null,
        IReadOnlyList<int>? byMonthDay = null,
        RecurrenceEndKind endKind = RecurrenceEndKind.Never,
        DateOnly? until = null,
        int? count = null)
    {
        if (interval < 1 || interval > CommunityLifeLimits.MaxRecurrenceInterval)
        {
            return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceIntervalInvalid);
        }

        var weekdays = Canonical(byWeekday, value => value is >= 1 and <= 7);

        if (weekdays is null)
        {
            return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceWeekdayInvalid);
        }

        var monthDays = Canonical(
            byMonthDay, value => value is >= -31 and <= 31 && value != 0);

        if (monthDays is null)
        {
            return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceMonthDayInvalid);
        }

        // A weekday list on a monthly rule, or a month-day list on a weekly one, is not a partial
        // truth the expander can honour — it is a rule that means two different things. Dropped
        // rather than half-applied, so the stored rule and the generated dates always agree.
        if (frequency != RecurrenceFrequency.Weekly)
        {
            weekdays = string.Empty;
        }

        if (frequency != RecurrenceFrequency.Monthly)
        {
            monthDays = string.Empty;
        }

        switch (endKind)
        {
            case RecurrenceEndKind.OnDate when until is null:
                return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceEndInvalid);

            case RecurrenceEndKind.AfterCount
                when count is not { } total
                    || total < 1
                    || total > CommunityLifeLimits.MaxSeriesOccurrences:
                return Result.Failure<RecurrenceRule>(CommunityLifeErrors.RecurrenceCountInvalid);

            case RecurrenceEndKind.Never:
            default:
                break;
        }

        return Result.Success(new RecurrenceRule(
            frequency,
            interval,
            weekdays,
            monthDays,
            endKind,
            endKind == RecurrenceEndKind.OnDate ? until : null,
            endKind == RecurrenceEndKind.AfterCount ? count : null));
    }

    /// <summary>
    /// The rule in words, rendered by the server.
    /// <para>
    /// <b>The client shows this rather than composing its own sentence</b>, because the server owns
    /// the expansion semantics and a sentence written next to a different set of rules drifts from
    /// them the first time either changes. English only, and that is an honest limitation recorded
    /// in the report rather than a hidden one: localisation of this string belongs with whatever
    /// phase gives the platform a localisation seam for server-rendered prose.
    /// </para>
    /// </summary>
    public string Describe(DateOnly firstDate)
    {
        // "Every" for an interval of one, "Every 2nd" above it. One expression, because composing
        // the cadence in two places is how the first attempt produced "Every every week on Monday"
        // — which the unit tests caught before anybody read it on a screen.
        var cadence = Interval == 1 ? "Every" : $"Every {Ordinal(Interval)}";

        var body = Frequency switch
        {
            RecurrenceFrequency.Daily => Interval == 1 ? "Every day" : $"Every {Interval} days",
            RecurrenceFrequency.Weekly => $"{cadence} week on {WeekdayNames(firstDate)}",
            RecurrenceFrequency.Monthly => $"{cadence} month on {MonthDayNames(firstDate)}",
            RecurrenceFrequency.Yearly => $"{cadence} year on "
                + firstDate.ToString("d MMMM", CultureInfo.InvariantCulture),
            _ => "Repeats"
        };

        return EndKind switch
        {
            RecurrenceEndKind.OnDate when Until is { } until =>
                $"{body}, until {until.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}",
            RecurrenceEndKind.AfterCount when Count is { } count =>
                $"{body}, {count} {(count == 1 ? "time" : "times")}",
            _ => body
        };
    }

    /// <summary>
    /// The number of occurrences the rule can ever produce, when that is knowable.
    /// <para>
    /// Null for a rule that never ends and for one that ends on a date, because the second depends
    /// on the calendar rather than on the rule. Used only to bound expansion.
    /// </para>
    /// </summary>
    public int? TotalOccurrences =>
        EndKind == RecurrenceEndKind.AfterCount ? Count : null;

    private static string? Canonical(IReadOnlyList<int>? values, Func<int, bool> isValid)
    {
        if (values is not { Count: > 0 })
        {
            return string.Empty;
        }

        if (values.Any(value => !isValid(value)))
        {
            return null;
        }

        return string.Join(
            Separator,
            values
                .Distinct()
                .OrderBy(value => value)
                .Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private static IReadOnlyList<int> ParseNumbers(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return
        [
            .. value
                .Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => int.TryParse(
                    part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : (int?)null)
                .OfType<int>()
        ];
    }

    /// <summary>
    /// ISO 1–7 to <see cref="DayOfWeek"/>, whose 0 is Sunday. The off-by-one between the two
    /// numberings is the single likeliest bug in any recurrence implementation, so the conversion
    /// exists once.
    /// </summary>
    private static IReadOnlyList<DayOfWeek> ParseWeekdays(string? value) =>
        [.. ParseNumbers(value).Select(day => (DayOfWeek)(day % 7))];

    private string WeekdayNames(DateOnly firstDate)
    {
        var days = Weekdays.Count > 0 ? Weekdays : [firstDate.DayOfWeek];

        return string.Join(
            ", ",
            days
                .OrderBy(day => (int)day == 0 ? 7 : (int)day)
                .Select(day => CultureInfo.InvariantCulture.DateTimeFormat.GetDayName(day)));
    }

    private string MonthDayNames(DateOnly firstDate)
    {
        var days = MonthDays.Count > 0 ? MonthDays : [firstDate.Day];

        return string.Join(
            ", ",
            days.Select(day => day < 0
                ? day == -1 ? "the last day" : $"the {Ordinal(-day)} last day"
                : $"the {Ordinal(day)}"));
    }

    private static string Ordinal(int value)
    {
        var suffix = (value % 100) is >= 11 and <= 13
            ? "th"
            : (value % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

        return value.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Frequency;
        yield return Interval;
        yield return ByWeekday;
        yield return ByMonthDay;
        yield return EndKind;
        yield return Until;
        yield return Count;
    }
}
