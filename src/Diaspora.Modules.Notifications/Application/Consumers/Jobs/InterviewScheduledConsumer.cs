using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Jobs;

/// <summary>
/// An interview was scheduled or moved.
/// <para>
/// <b>The highest-stakes message the platform sends.</b> A candidate who misses it has lost the job,
/// and a cross-border candidate may be booking a visa appointment or a flight against it. Two
/// triggers come out of one event: the confirmation now, and a reminder the rule schedules against
/// the interview time — which is why <c>SubjectMoment</c> exists and why the reminder rule can say
/// "the day before" without any code here knowing what an interview is.
/// </para>
/// <para>
/// <c>IsReschedule</c> is passed through as a value because "moved to Thursday" and "you have an
/// interview on Thursday" are different messages, and sending the second one twice is how somebody
/// turns up on the wrong day.
/// </para>
/// </summary>
public sealed class InterviewScheduledConsumer
    : ModuleEventConsumer<InterviewScheduledIntegrationEvent>
{
    public InterviewScheduledConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Jobs";

    protected override IEnumerable<NotificationTrigger> Translate(
        InterviewScheduledIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["jobTitle"] = integrationEvent.JobTitle;
        values["employerName"] = integrationEvent.EmployerName;
        values["mode"] = integrationEvent.Mode;
        values["scheduledAt"] = integrationEvent.ScheduledAt;
        values["durationMinutes"] = integrationEvent.DurationMinutes;
        values["roundNumber"] = integrationEvent.RoundNumber;
        values["isReschedule"] = integrationEvent.IsReschedule;
        values["employerTimeZoneId"] = integrationEvent.TimeZoneId ?? string.Empty;
        values["jobId"] = integrationEvent.JobId;
        values["applicationId"] = integrationEvent.ApplicationId;

        // Events published before B13.1 carry the empty identifier: the contract required a member
        // and the relay wrote a placeholder, so the platform's highest-stakes message was addressed
        // to nobody. A replay of one of those must produce nothing rather than an inbox for a
        // member that does not exist.
        if (integrationEvent.CandidateUserId == Guid.Empty)
        {
            yield break;
        }

        var candidate = UserId.From(integrationEvent.CandidateUserId);

        // The confirmation, now.
        yield return Trigger(
            candidate,
            "candidate",
            integrationEvent.OccurredAt,
            values,
            subjectType: "interview",
            subjectId: integrationEvent.InterviewId.ToString(),
            subjectMoment: integrationEvent.ScheduledAt);

        // The reminder, whenever the rule says relative to the interview itself.
        yield return Trigger(
            candidate,
            "candidate-reminder",
            integrationEvent.OccurredAt,
            values,
            subjectType: "interview",
            subjectId: integrationEvent.InterviewId.ToString(),
            subjectMoment: integrationEvent.ScheduledAt);
    }
}
