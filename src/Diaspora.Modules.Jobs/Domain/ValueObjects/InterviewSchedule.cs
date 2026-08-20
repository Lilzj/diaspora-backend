using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// When and how an interview happens.
/// <para>
/// <b>The mode decides which detail is required, and the validation enforces it.</b> An on-site
/// interview without an address and a video interview without a joining link are both
/// appointments a candidate cannot attend, and the moment to catch that is when the employer
/// schedules it rather than an hour beforehand. A cross-border candidate told only "Tuesday at
/// ten" cannot tell whether they need a visa appointment.
/// </para>
/// <para>
/// <b>The IANA time zone is stored beside the instant, and it is not redundant.</b> The instant
/// says unambiguously when; the zone says which wall clock the employer meant, which is what a
/// candidate three time zones away needs to see rendered — "14:00 your time (16:00 in Berlin)".
/// Storing only the instant loses the employer's intent and makes every reschedule an
/// arithmetic problem for a person.
/// </para>
/// </summary>
public sealed class InterviewSchedule : ValueObject
{
    private InterviewSchedule(
        DateTimeOffset scheduledAt,
        int durationMinutes,
        string? timeZoneId,
        InterviewMode mode,
        string? locationAddress,
        string? joiningUrl,
        string? instructions)
    {
        ScheduledAt = scheduledAt;
        DurationMinutes = durationMinutes;
        TimeZoneId = timeZoneId;
        Mode = mode;
        LocationAddress = locationAddress;
        JoiningUrl = joiningUrl;
        Instructions = instructions;
    }

    private InterviewSchedule() { }

    public DateTimeOffset ScheduledAt { get; private set; }

    public int DurationMinutes { get; private set; }

    /// <summary>An IANA zone identifier — the wall clock the employer meant.</summary>
    public string? TimeZoneId { get; private set; }

    public InterviewMode Mode { get; private set; }

    /// <summary>Where to go. Required for an on-site interview.</summary>
    public string? LocationAddress { get; private set; }

    /// <summary>How to join. Required for a phone, video, panel or technical interview.</summary>
    public string? JoiningUrl { get; private set; }

    /// <summary>Anything else the candidate needs — what to bring, who to ask for.</summary>
    public string? Instructions { get; private set; }

    public static Result<InterviewSchedule> Create(
        DateTimeOffset scheduledAt,
        int durationMinutes,
        InterviewMode mode,
        DateTimeOffset now,
        string? timeZoneId = null,
        string? locationAddress = null,
        string? joiningUrl = null,
        string? instructions = null)
    {
        if (scheduledAt <= now)
        {
            return Result.Failure<InterviewSchedule>(JobErrors.InterviewInPast);
        }

        if (durationMinutes < JobLimits.MinInterviewMinutes
            || durationMinutes > JobLimits.MaxInterviewMinutes)
        {
            return Result.Failure<InterviewSchedule>(JobErrors.InterviewDurationInvalid);
        }

        if (!Enum.IsDefined(mode))
        {
            return Result.Failure<InterviewSchedule>(JobErrors.InterviewLocationInvalid);
        }

        var address = Clean(locationAddress);
        var url = Clean(joiningUrl);
        var notes = Clean(instructions);

        if (address is { Length: > JobLimits.MaxLocationName }
            || notes is { Length: > JobLimits.MaxInterviewNote })
        {
            return Result.Failure<InterviewSchedule>(JobErrors.InterviewLocationInvalid);
        }

        if (url is not null)
        {
            if (url.Length > JobLimits.MaxUrl
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Result.Failure<InterviewSchedule>(JobErrors.InterviewLocationInvalid);
            }

            url = uri.ToString();
        }

        // The mode's own requirement. An assessment needs neither: it is unattended, and the
        // instructions carry whatever the candidate has to do.
        var satisfied = mode switch
        {
            InterviewMode.OnSite => address is not null,
            InterviewMode.Assessment => true,
            InterviewMode.Phone => url is not null || notes is not null,
            _ => url is not null
        };

        if (!satisfied)
        {
            return Result.Failure<InterviewSchedule>(JobErrors.InterviewLocationInvalid);
        }

        return Result.Success(new InterviewSchedule(
            scheduledAt, durationMinutes, NormalizeZone(timeZoneId), mode, address, url, notes));
    }

    /// <summary>When the appointment ends.</summary>
    public DateTimeOffset EndsAt => ScheduledAt.AddMinutes(DurationMinutes);

    /// <summary>Whether the appointment is already behind us.</summary>
    public bool HasPassed(DateTimeOffset now) => EndsAt <= now;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ScheduledAt;
        yield return DurationMinutes;
        yield return TimeZoneId;
        yield return Mode;
        yield return LocationAddress;
        yield return JoiningUrl;
        yield return Instructions;
    }

    /// <summary>
    /// Keeps a zone identifier only if this machine recognises it. An unrecognised zone is
    /// dropped rather than rejected — the instant is still correct, and refusing to schedule an
    /// interview because a client sent an unfamiliar zone name would be a poor trade.
    /// </summary>
    private static string? NormalizeZone(string? timeZoneId)
    {
        var trimmed = Clean(timeZoneId);

        if (trimmed is null)
        {
            return null;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out _) ? trimmed : null;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
