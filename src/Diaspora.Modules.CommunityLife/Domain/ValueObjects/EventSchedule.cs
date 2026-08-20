using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.ValueObjects;

/// <summary>
/// When an event happens.
/// <para>
/// <b>Instants plus an IANA time-zone identifier, not a local date and a local time.</b> The
/// instants are what every comparison in the module runs against — is it over, is it open, does it
/// clash — and the zone identifier is what a client needs to render "7pm" to somebody in the city
/// where it is happening rather than "7pm" to somebody reading from another continent. Storing only
/// the local pair would make every one of those comparisons wrong across a daylight-saving
/// boundary; storing only the instants would make a diaspora platform display every event in the
/// reader's own time, which is exactly backwards for an event they have to travel to.
/// </para>
/// <para>
/// <b>The registration deadline lives here rather than beside the capacity</b>, because it is a
/// point on the same timeline and every rule that reads it also reads the start. It defaults to the
/// start time when the organizer does not set one, which is what an organizer means by leaving it
/// blank.
/// </para>
/// </summary>
public sealed class EventSchedule : ValueObject
{
    private EventSchedule(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string timeZoneId,
        DateTimeOffset? registrationDeadline,
        DateTimeOffset? checkInOpensAt)
    {
        StartsAt = startsAt;
        EndsAt = endsAt;
        TimeZoneId = timeZoneId;
        RegistrationDeadline = registrationDeadline;
        CheckInOpensAt = checkInOpensAt;
    }

    private EventSchedule() { }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>
    /// An IANA identifier — <c>Europe/London</c>, <c>Africa/Lagos</c>. Stored as text rather than
    /// as an offset because an offset is wrong twice a year.
    /// </summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>When responses close. Defaults to the start time.</summary>
    public DateTimeOffset? RegistrationDeadline { get; private set; }

    /// <summary>
    /// When the door opens for attendance. Defaults to an hour before the start, which is what an
    /// organizer means when they do not say — nobody sets up on the minute.
    /// </summary>
    public DateTimeOffset? CheckInOpensAt { get; private set; }

    public TimeSpan Duration => EndsAt - StartsAt;

    public bool HasStarted(DateTimeOffset now) => now >= StartsAt;

    public bool HasEnded(DateTimeOffset now) => now >= EndsAt;

    public bool IsRunning(DateTimeOffset now) => now >= StartsAt && now < EndsAt;

    /// <summary>Whether responses are still open on the clock alone.</summary>
    public bool AcceptsRegistrationAt(DateTimeOffset now) =>
        now <= (RegistrationDeadline ?? StartsAt);

    /// <summary>Whether the door is open on the clock alone.</summary>
    public bool AcceptsCheckInAt(DateTimeOffset now) =>
        now >= (CheckInOpensAt ?? StartsAt.AddHours(-1)) && now <= EndsAt.AddHours(12);

    public static Result<EventSchedule> Create(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        string? timeZoneId,
        DateTimeOffset? registrationDeadline = null,
        DateTimeOffset? checkInOpensAt = null,
        DateTimeOffset? now = null)
    {
        if (endsAt <= startsAt)
        {
            return Result.Failure<EventSchedule>(CommunityLifeErrors.ScheduleInvalid);
        }

        if (endsAt - startsAt > TimeSpan.FromDays(CommunityLifeLimits.MaxDurationDays))
        {
            return Result.Failure<EventSchedule>(CommunityLifeErrors.ScheduleTooLong);
        }

        if (now is { } reference)
        {
            if (endsAt <= reference)
            {
                return Result.Failure<EventSchedule>(CommunityLifeErrors.ScheduleInPast);
            }

            if (startsAt - reference
                > TimeSpan.FromDays(CommunityLifeLimits.MaxScheduleHorizonDays))
            {
                return Result.Failure<EventSchedule>(CommunityLifeErrors.ScheduleTooFarAhead);
            }
        }

        var zone = NormalizeZone(timeZoneId);

        if (zone is null)
        {
            return Result.Failure<EventSchedule>(CommunityLifeErrors.TimeZoneInvalid);
        }

        if (registrationDeadline is { } deadline && deadline > endsAt)
        {
            return Result.Failure<EventSchedule>(
                CommunityLifeErrors.RegistrationDeadlineInvalid);
        }

        return Result.Success(new EventSchedule(
            startsAt, endsAt, zone, registrationDeadline, checkInOpensAt));
    }

    /// <summary>Moves the event, keeping the zone and the deadlines relative to the new start.</summary>
    public Result<EventSchedule> Reschedule(
        DateTimeOffset startsAt, DateTimeOffset endsAt, DateTimeOffset? now = null)
    {
        var shift = startsAt - StartsAt;

        return Create(
            startsAt,
            endsAt,
            TimeZoneId,
            RegistrationDeadline is { } deadline ? deadline + shift : null,
            CheckInOpensAt is { } opens ? opens + shift : null,
            now);
    }

    /// <summary>
    /// Whether the identifier names a zone this deployment knows about.
    /// <para>
    /// <c>TimeZoneInfo.FindSystemTimeZoneById</c> rather than a hard-coded list, so the answer
    /// tracks the operating system's own tzdata. On .NET 6 and later it accepts IANA identifiers on
    /// Windows too, which is what makes one stored string correct on every host the platform runs
    /// on.
    /// </para>
    /// </summary>
    private static string? NormalizeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return "UTC";
        }

        var trimmed = timeZoneId.Trim();

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(trimmed).Id is not null ? trimmed : null;
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
        yield return StartsAt;
        yield return EndsAt;
        yield return TimeZoneId;
        yield return RegistrationDeadline;
        yield return CheckInOpensAt;
    }
}
