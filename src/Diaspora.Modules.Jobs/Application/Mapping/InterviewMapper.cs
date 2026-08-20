using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Interviews;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns an interview into the shape the wire carries.
/// <para>
/// <paramref name="forEmployer"/> gates the panel's notes. <c>Feedback</c> is what the
/// interviewers wrote about a named person and is never sent to them; <c>CandidateFeedback</c> is
/// the version the employer chose to share. Deciding it here means no route can leak the first by
/// forgetting.
/// </para>
/// </summary>
public static class InterviewMapper
{
    public static InterviewResponse ToResponse(
        Interview interview, string jobTitle, string employerName, bool forEmployer)
    {
        ArgumentNullException.ThrowIfNull(interview);

        return new InterviewResponse
        {
            InterviewId = interview.Id.Value,
            ApplicationId = interview.ApplicationId.Value,
            JobId = interview.PostingId.Value,
            JobTitle = jobTitle,
            EmployerName = employerName,
            RoundNumber = interview.RoundNumber,
            Title = interview.Title,
            Mode = JobWireNames.ToWire(interview.Schedule.Mode),
            ScheduledAt = interview.Schedule.ScheduledAt,
            DurationMinutes = interview.Schedule.DurationMinutes,
            TimeZoneId = interview.Schedule.TimeZoneId,
            LocationAddress = interview.Schedule.LocationAddress,
            JoiningUrl = interview.Schedule.JoiningUrl,
            Instructions = interview.Schedule.Instructions,

            Participants = [.. interview.Participants.Select(participant =>
                new InterviewParticipantResponse
                {
                    Name = participant.Name,
                    Role = participant.Role
                })],

            Outcome = JobWireNames.ToWire(interview.Outcome),
            IsCancelled = interview.IsCancelled,
            CancellationReason = interview.CancellationReason,

            // Surfaced to both. An employer who has moved an interview four times has told the
            // candidate something, and hiding it would be the platform helping them hide it.
            RescheduleCount = interview.RescheduleCount,

            CompletedAt = interview.CompletedAt,

            // Employer only. See the class summary.
            Feedback = forEmployer ? interview.Feedback : null,
            CandidateFeedback = interview.CandidateFeedback,

            Revision = interview.Revision
        };
    }
}
