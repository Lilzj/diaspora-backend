using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// An interview was scheduled or moved.
/// <para>
/// <b>The highest-stakes notification the platform sends.</b> A candidate who misses this has
/// lost the job, and a cross-border candidate may need to book a visa appointment or a flight
/// against it. It carries the joining detail and the employer's own time zone so a message can
/// render both clocks — "14:00 your time, 16:00 in Berlin" — rather than an instant the reader
/// has to convert.
/// </para>
/// <para>
/// <see cref="IsReschedule"/> exists so the message can say which it is. "Moved to Thursday" and
/// "you have an interview on Thursday" are different messages, and sending the second one twice
/// is how somebody turns up on the wrong day.
/// </para>
/// </summary>
public sealed record InterviewScheduledIntegrationEvent : IntegrationEvent
{
    public required Guid InterviewId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>
    /// The member, so the notification can be addressed.
    /// <para>
    /// Events published before B13.1 carry <see cref="Guid.Empty"/> here: the relay had no route
    /// from an interview to the applying member and wrote a placeholder, which is why the
    /// highest-stakes message on the platform was addressed to nobody. A consumer must treat an
    /// empty value as absent rather than as a member.
    /// </para>
    /// </summary>
    public required Guid CandidateUserId { get; init; }

    public required string JobTitle { get; init; }

    public required string EmployerName { get; init; }

    /// <summary><c>Phone</c>, <c>Video</c>, <c>OnSite</c>, <c>Panel</c>, <c>Technical</c>, <c>Assessment</c>.</summary>
    public required string Mode { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>The employer's IANA wall clock, so a message can render both time zones.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Which round. A candidate deserves to know it is the third.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>False the first time, true on every move afterwards.</summary>
    public required bool IsReschedule { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// The employer booked it, so the confirmation is not addressed to them. It is carried so a
    /// reminder rule can address both sides of an appointment from one contract: an interviewer
    /// who forgets is as costly to the candidate as a candidate who forgets.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
