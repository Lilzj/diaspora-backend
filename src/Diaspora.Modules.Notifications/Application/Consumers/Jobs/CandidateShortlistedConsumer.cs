using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Jobs;

/// <summary>
/// A candidate was shortlisted.
/// <para>
/// The one hiring transition worth its own subscription, and the reason Jobs publishes it separately
/// from the general status change: it is the first unambiguously good news in a process that is
/// otherwise mostly silence.
/// </para>
/// <para>
/// <b>The empty-identifier guard is not defensive coding, it is a replay guard.</b> Every event of
/// this type published before B13.1 carries <see cref="Guid.Empty"/> in <c>ApplicantUserId</c> —
/// the contract required a member and the relay had no route to one. Those events are still in
/// outbox tables and still replayable, and a trigger addressed to the empty identifier would create
/// an inbox for a member that does not exist.
/// </para>
/// </summary>
public sealed class CandidateShortlistedConsumer
    : ModuleEventConsumer<CandidateShortlistedIntegrationEvent>
{
    public CandidateShortlistedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Jobs";

    protected override IEnumerable<NotificationTrigger> Translate(
        CandidateShortlistedIntegrationEvent integrationEvent)
    {
        if (integrationEvent.ApplicantUserId == Guid.Empty)
        {
            yield break;
        }

        var values = Values();
        values["jobTitle"] = integrationEvent.JobTitle;
        values["employerName"] = integrationEvent.EmployerName;
        values["jobId"] = integrationEvent.JobId;
        values["applicationId"] = integrationEvent.ApplicationId;

        yield return Trigger(
            UserId.From(integrationEvent.ApplicantUserId),
            "candidate",
            integrationEvent.ShortlistedAt,
            values,
            subjectType: "application",
            subjectId: integrationEvent.ApplicationId.ToString());
    }
}
