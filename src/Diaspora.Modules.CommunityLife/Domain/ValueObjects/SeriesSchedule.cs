using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// What turns a recurring <em>date</em> into an actual instant.
/// <para>
/// <b>A local time of day plus an IANA zone, and emphatically not a UTC offset or a stored
/// instant.</b> This is the single decision that makes a recurring event correct across a
/// daylight-saving boundary. A weekly meeting at seven in the evening in London is at seven in the
/// evening in London in January and in July; its UTC instant differs by an hour between the two, so
/// a series that generated occurrences by adding 168 hours to the first one would silently move the
/// meeting to six o'clock every spring and nobody would find out until a room was empty.
/// </para>
/// <para>
/// <b>The two transitions are handled explicitly rather than left to the framework.</b>
/// <c>TimeZoneInfo.ConvertTimeToUtc</c> throws on a local time that does not exist — the hour the
/// clocks skip forward — and silently picks standard time for one that happens twice. Neither
/// default is acceptable in a generator: the first would abort an expansion mid-series, and the
/// second would move an occurrence an hour later than every other one in its series. See
/// <see cref="ToStart"/>.
/// </para>
/// <para>
/// <b>The duration is minutes rather than an end time</b>, because an event that runs from 23:00 to
/// 01:00 crosses midnight and a stored end <em>time</em> would compare as being before its start.
/// </para>
/// </summary>
public sealed class SeriesSchedule : ValueObject
{
    private SeriesSchedule(
        DateOnly firstLocalDate, TimeOnly localStartTime, int durationMinutes, string timeZoneId)
    {
        FirstLocalDate = firstLocalDate;
        LocalStartTime = localStartTime;
        DurationMinutes = durationMinutes;
        TimeZoneId = timeZoneId;
    }

    private SeriesSchedule() { }

    /// <summary>The local date the first occurrence falls on, or the earliest the rule may pick from.</summary>
    public DateOnly FirstLocalDate { get; private set; }

    /// <summary>The local wall-clock time every occurrence starts at.</summary>
    public TimeOnly LocalStartTime { get; private set; }

    /// <summary>How long each occurrence runs. Minutes, so an event may cross midnight.</summary>
    public int DurationMinutes { get; private set; }

    /// <summary>An IANA identifier — <c>Europe/London</c>, <c>Africa/Lagos</c>.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes);

    public static Result<SeriesSchedule> Create(
        DateOnly firstLocalDate,
        TimeOnly localStartTime,
        int durationMinutes,
        string? timeZoneId)
    {
        if (durationMinutes < 1
            || durationMinutes > CommunityLifeLimits.MaxDurationDays * 24 * 60)
        {
            return Result.Failure<SeriesSchedule>(CommunityLifeErrors.ScheduleInvalid);
        }

        var zone = ResolveZone(timeZoneId);

        return zone is null
            ? Result.Failure<SeriesSchedule>(CommunityLifeErrors.TimeZoneInvalid)
            : Result.Success(
                new SeriesSchedule(firstLocalDate, localStartTime, durationMinutes, zone.Id));
    }

    /// <summary>The same schedule on a different first date. What "this and future" rebases with.</summary>
    public SeriesSchedule StartingOn(DateOnly firstLocalDate) =>
        new(firstLocalDate, LocalStartTime, DurationMinutes, TimeZoneId);

    /// <summary>
    /// The UTC instant an occurrence on a given local date begins.
    /// <para>
    /// <b>Skipped hour.</b> When the clocks go forward, a local time inside the gap never happens.
    /// The occurrence is moved forward by the size of the jump, which puts an 02:30 meeting at 03:30
    /// on that one morning — the behaviour every calendar application has, and the only one that
    /// keeps the occurrence on its own day.
    /// </para>
    /// <para>
    /// <b>Repeated hour.</b> When the clocks go back, a local time inside the overlap happens twice.
    /// The <em>earlier</em> instant is taken — the daylight offset, which is the larger one — so the
    /// occurrence keeps the same gap from the one before it rather than gaining an hour.
    /// </para>
    /// </summary>
    public DateTimeOffset ToStart(DateOnly localDate)
    {
        var zone = ResolveZone(TimeZoneId) ?? TimeZoneInfo.Utc;
        var local = localDate.ToDateTime(LocalStartTime, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            // The size of the jump, measured either side of it. Reading the rule's own delta would
            // mean walking TimeZoneInfo's adjustment rules; a day either side is the same number
            // and needs no assumption about how the rule is expressed.
            var gap = zone.GetUtcOffset(local.AddDays(1)) - zone.GetUtcOffset(local.AddDays(-1));

            local = local.Add(gap <= TimeSpan.Zero ? TimeSpan.FromHours(1) : gap);
        }

        var offset = zone.IsAmbiguousTime(local)
            ? zone.GetAmbiguousTimeOffsets(local).Max()
            : zone.GetUtcOffset(local);

        return new DateTimeOffset(local, offset);
    }

    /// <summary>The instant an occurrence on a given local date ends.</summary>
    public DateTimeOffset ToEnd(DateOnly localDate) => ToStart(localDate).AddMinutes(DurationMinutes);

    /// <summary>
    /// The local date an instant falls on in this series' zone.
    /// <para>
    /// Used to turn an occurrence's stored start back into the rule's own vocabulary — an exception
    /// is a <em>date</em>, and asking "which date was that" in UTC would put a 23:00 Lagos event on
    /// the following day.
    /// </para>
    /// </summary>
    public DateOnly ToLocalDate(DateTimeOffset instant)
    {
        var zone = ResolveZone(TimeZoneId) ?? TimeZoneInfo.Utc;

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }

    /// <summary>
    /// Whether the identifier names a zone this deployment knows about.
    /// <para>
    /// <c>FindSystemTimeZoneById</c> rather than a hard-coded list, so the answer tracks the host's
    /// own tzdata. On .NET 6 and later it accepts IANA identifiers on Windows too, which is what
    /// makes one stored string correct on every host the platform runs on — the same reasoning
    /// <see cref="EventSchedule"/> records.
    /// </para>
    /// </summary>
    private static TimeZoneInfo? ResolveZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
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

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FirstLocalDate;
        yield return LocalStartTime;
        yield return DurationMinutes;
        yield return TimeZoneId;
    }
}
