using Diaspora.Modules.Analytics.Domain.Enums;

namespace Diaspora.Modules.Analytics.Domain.Periods;

/// <summary>
/// A resolved reporting window.
/// <para>
/// <b>Half-open — <c>[Start, End)</c> — everywhere, without exception.</b> The alternative is a
/// closed range, and a closed range makes the last instant of one period the first instant of the
/// next: an event at exactly midnight is counted in both, every comparison double-counts its
/// boundary, and the error is one row in a thousand, which is small enough that nobody finds it and
/// large enough to make two dashboards disagree. Every query in this module filters
/// <c>OccurredAt &gt;= Start &amp;&amp; OccurredAt &lt; End</c> and a unit test asserts the
/// boundary instant lands on exactly one side.
/// </para>
/// <para>
/// <b>The window is cut in a stated timezone and stored in UTC.</b> An instant is the wrong wire
/// format for a period — <c>2026-08-01T00:00:00Z</c> begins at noon in Auckland and at five in the
/// afternoon of the previous day in Los Angeles, and two dashboards built on it both say "1–8
/// August" while showing different data. The client sends calendar dates and a zone; this type
/// turns them into instants exactly once.
/// </para>
/// <para>
/// <b>Nothing here reads the machine clock.</b> <c>UtcNow</c> arrives as an argument, which is what
/// makes a midnight-boundary test a test rather than a coin toss.
/// </para>
/// </summary>
public sealed record AnalyticsPeriod
{
    private AnalyticsPeriod(
        AnalyticsPeriodPreset preset,
        DateTimeOffset start,
        DateTimeOffset end,
        DateOnly fromDate,
        DateOnly toDate,
        string timeZoneId,
        AnalyticsGranularity granularity)
    {
        Preset = preset;

        // ── Instants are stored in UTC, always ──────────────────────────────
        //
        // `Start` and `End` are computed in the caller's zone, so a request from
        // Lagos produced a `DateTimeOffset` carrying `+01:00`. Those boundaries
        // are handed to EF as query parameters, and Npgsql refuses them:
        //
        //     Cannot write DateTimeOffset with Offset=01:00:00 to PostgreSQL
        //     type 'timestamp with time zone', only offset 0 (UTC) is supported.
        //
        // The result was a **500 on every analytics read from a device in any
        // zone but UTC** — which is every real device. It went unnoticed because
        // every probe and every curl against these routes passed `timezone=UTC`
        // or omitted it, and the default is UTC.
        //
        // `ToUniversalTime()` does not move the instant, only how it is written,
        // so `Contains`, `Duration` and `Previous()` are unchanged. The zone the
        // member asked for is not lost: it is `TimeZoneId`, which is what
        // `FromDate`/`ToDate` were computed in and what the response echoes.
        Start = start.ToUniversalTime();
        End = end.ToUniversalTime();

        FromDate = fromDate;
        ToDate = toDate;
        TimeZoneId = timeZoneId;
        Granularity = granularity;
    }

    public AnalyticsPeriodPreset Preset { get; }

    /// <summary>Inclusive. The first instant inside the window.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>Exclusive. The first instant after the window.</summary>
    public DateTimeOffset End { get; }

    /// <summary>The first calendar day of the window, in <see cref="TimeZoneId"/>. Inclusive.</summary>
    public DateOnly FromDate { get; }

    /// <summary>The last calendar day of the window, in <see cref="TimeZoneId"/>. Inclusive.</summary>
    public DateOnly ToDate { get; }

    /// <summary>The IANA zone the days were cut in. Never assumed, always stated back to the client.</summary>
    public string TimeZoneId { get; }

    /// <summary>How the server buckets the series. The client never regroups.</summary>
    public AnalyticsGranularity Granularity { get; }

    public TimeSpan Duration => End - Start;

    /// <summary>
    /// The baseline this period is compared against.
    /// <para>
    /// <b>The same duration, immediately preceding, and never a calendar month against thirty
    /// days.</b> §46's example is the rule: 15 January to 15 February compares against 15 December
    /// to 15 January. A calendar-month baseline under a thirty-day period would make February look
    /// like a collapse every year.
    /// </para>
    /// </summary>
    public AnalyticsPeriod Previous()
    {
        var duration = Duration;
        var start = Start - duration;

        return new AnalyticsPeriod(
            Preset,
            start,
            Start,
            FromDate.AddDays(-(ToDate.DayNumber - FromDate.DayNumber + 1)),
            FromDate.AddDays(-1),
            TimeZoneId,
            Granularity);
    }

    /// <summary>True when the instant falls inside the window. Half-open, as the type promises.</summary>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant < End;

    /// <summary>
    /// Resolves a request into a window.
    /// <para>
    /// Every refusal is a named error rather than a silent correction. An inverted custom range is
    /// the one worth stating: swapping the dates would answer a question the caller did not ask, and
    /// the client's period selector already handles the refusal.
    /// </para>
    /// </summary>
    public static AnalyticsPeriodResolution Resolve(
        AnalyticsPeriodPreset preset,
        DateOnly? from,
        DateOnly? to,
        TimeZoneInfo zone,
        DateTimeOffset utcNow,
        int maximumCustomDays)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);

        DateOnly first;
        DateOnly last;

        switch (preset)
        {
            case AnalyticsPeriodPreset.Today:
                first = today;
                last = today;
                break;

            case AnalyticsPeriodPreset.Last7Days:
                first = today.AddDays(-6);
                last = today;
                break;

            case AnalyticsPeriodPreset.Last30Days:
                first = today.AddDays(-29);
                last = today;
                break;

            case AnalyticsPeriodPreset.Last90Days:
                first = today.AddDays(-89);
                last = today;
                break;

            case AnalyticsPeriodPreset.Year:
                // Twelve calendar months ending with the current one, which is what "12 monthly
                // buckets" means. A rolling 365 days would put a partial month at both ends and
                // make every month label a lie.
                first = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
                last = today;
                break;

            case AnalyticsPeriodPreset.Custom:
                if (from is null || to is null)
                {
                    return AnalyticsPeriodResolution.Incomplete();
                }

                if (from > to)
                {
                    return AnalyticsPeriodResolution.Inverted();
                }

                if (to.Value.DayNumber - from.Value.DayNumber + 1 > maximumCustomDays)
                {
                    return AnalyticsPeriodResolution.TooLong(maximumCustomDays);
                }

                first = from.Value;
                last = to.Value;
                break;

            default:
                return AnalyticsPeriodResolution.UnsupportedPreset();
        }

        var start = ToInstant(first, zone);

        // The exclusive end is the start of the day *after* the last day, so the last day is whole.
        var end = ToInstant(last.AddDays(1), zone);

        return AnalyticsPeriodResolution.Resolved(
            new AnalyticsPeriod(preset, start, end, first, last, zone.Id, GranularityFor(preset, first, last)));
    }

    /// <summary>
    /// Midnight at the start of a calendar day, in a zone, as an instant.
    /// <para>
    /// <b>Daylight saving is why this is a method rather than an expression.</b> On the spring-
    /// forward day in some zones midnight does not exist, and <c>TimeZoneInfo</c> reports the
    /// nominal time as invalid; the correct reading of "the day began" is then the instant the
    /// offset changed. Constructing the offset from the *nominal* local time without checking would
    /// place the day's start an hour inside the previous day, and every figure for that one day in
    /// the year would be shifted by an hour.
    /// </para>
    /// </summary>
    private static DateTimeOffset ToInstant(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        if (!zone.IsInvalidTime(local))
        {
            return new DateTimeOffset(local, zone.GetUtcOffset(local));
        }

        // Walk forward to the first legal minute of the day. At most a two-hour gap in practice.
        for (var minutes = 1; minutes <= 180; minutes++)
        {
            var candidate = local.AddMinutes(minutes);

            if (!zone.IsInvalidTime(candidate))
            {
                return new DateTimeOffset(candidate, zone.GetUtcOffset(candidate));
            }
        }

        return new DateTimeOffset(local, TimeSpan.Zero);
    }

    /// <summary>
    /// How the server buckets a window.
    /// <para>
    /// The Phase 7 register's table for four of the five presets — daily for a week and a month,
    /// weekly for a quarter, monthly for a year — and length-derived for a custom range, so a chart
    /// never has to draw four hundred points.
    /// </para>
    /// <para>
    /// <b>The fifth is a deliberate deviation, stated rather than quietly different.</b> That table
    /// asks for twenty-four hourly buckets on <c>today</c>, and the rollup grain on this platform is
    /// a day: an hourly series would need either a second rollup grain maintained for one preset, or
    /// a per-hour grouping over the raw store using a date function that translates differently on
    /// every provider. Neither is worth it for a chart whose every figure is already in the tiles
    /// above it. <c>today</c> is served as one daily bucket, the series says <c>day</c>, and the
    /// client labels what it is given rather than assuming.
    /// </para>
    /// </summary>
    private static AnalyticsGranularity GranularityFor(
        AnalyticsPeriodPreset preset, DateOnly first, DateOnly last) => preset switch
    {
        AnalyticsPeriodPreset.Today
            or AnalyticsPeriodPreset.Last7Days
            or AnalyticsPeriodPreset.Last30Days => AnalyticsGranularity.Day,
        AnalyticsPeriodPreset.Last90Days => AnalyticsGranularity.Week,
        AnalyticsPeriodPreset.Year => AnalyticsGranularity.Month,
        _ => (last.DayNumber - first.DayNumber + 1) switch
        {
            <= 45 => AnalyticsGranularity.Day,
            <= 183 => AnalyticsGranularity.Week,
            _ => AnalyticsGranularity.Month
        }
    };
}

/// <summary>Why a period could not be resolved, or the period itself.</summary>
public sealed record AnalyticsPeriodResolution
{
    private AnalyticsPeriodResolution(
        AnalyticsPeriod? period, AnalyticsPeriodFailure failure, int limit)
    {
        Period = period;
        Failure = failure;
        Limit = limit;
    }

    public AnalyticsPeriod? Period { get; }

    public AnalyticsPeriodFailure Failure { get; }

    /// <summary>The cap, when the failure is <see cref="AnalyticsPeriodFailure.TooLong"/>.</summary>
    public int Limit { get; }

    public bool IsResolved => Period is not null;

    public static AnalyticsPeriodResolution Resolved(AnalyticsPeriod period) =>
        new(period, AnalyticsPeriodFailure.None, 0);

    public static AnalyticsPeriodResolution Incomplete() =>
        new(null, AnalyticsPeriodFailure.Incomplete, 0);

    public static AnalyticsPeriodResolution Inverted() =>
        new(null, AnalyticsPeriodFailure.Inverted, 0);

    public static AnalyticsPeriodResolution TooLong(int limit) =>
        new(null, AnalyticsPeriodFailure.TooLong, limit);

    public static AnalyticsPeriodResolution UnsupportedPreset() =>
        new(null, AnalyticsPeriodFailure.UnsupportedPreset, 0);
}

public enum AnalyticsPeriodFailure
{
    None = 0,

    /// <summary>A custom period arrived with only one bound.</summary>
    Incomplete = 1,

    /// <summary>A custom period arrived with its start after its end.</summary>
    Inverted = 2,

    /// <summary>The window is wider than this deployment answers for.</summary>
    TooLong = 3,

    /// <summary>The preset is not one this deployment serves.</summary>
    UnsupportedPreset = 4
}
