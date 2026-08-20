using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an interview is scheduled or rescheduled.
/// <para>
/// The notification this produces is the highest-stakes one the module sends: a candidate who
/// misses it has lost the job. <see cref="IsReschedule"/> exists so the message can say so —
/// "moved to Thursday" and "you have an interview on Thursday" are different messages, and
/// sending the second one twice is how someone turns up on the wrong day.
/// </para>
/// </summary>
public sealed record InterviewScheduledDomainEvent : DomainEvent
{
    public required InterviewId InterviewId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    public required InterviewMode Mode { get; init; }

    public required DateTimeOffset ScheduledAt { get; init; }

    public required int DurationMinutes { get; init; }

    /// <summary>The employer's wall clock, so a message can render both time zones.</summary>
    public string? TimeZoneId { get; init; }

    /// <summary>Which round this is. A candidate deserves to know it is the third.</summary>
    public required int RoundNumber { get; init; }

    /// <summary>False the first time, true on every move afterwards.</summary>
    public required bool IsReschedule { get; init; }

    public UserId? ScheduledBy { get; init; }
}
