using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Jobs;

/// <summary>
/// A candidate applied.
/// <para>
/// <b>Two messages about one fact, and B13.1 is what made the second one possible.</b> The
/// candidate is acknowledged; the employer is told they have an application waiting. Until Jobs put
/// <c>EmployerUserId</c> on the contract this module could only send the first, because an employer
/// is an organisation and the payload named nobody at it — and resolving that here would have meant
/// Communication querying a business module, which is the coupling that does not exist.
/// </para>
/// <para>
/// <b>Neither trigger carries anything about the candidate beyond an identifier.</b> Jobs keeps
/// profile content off this contract deliberately; the recruiter's message says an application
/// arrived and leaves who it is from to the screen they open.
/// </para>
/// </summary>
public sealed class ApplicationSubmittedConsumer
    : ModuleEventConsumer<ApplicationSubmittedIntegrationEvent>
{
    public ApplicationSubmittedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Jobs";

    protected override IEnumerable<NotificationTrigger> Translate(
        ApplicationSubmittedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["jobId"] = integrationEvent.JobId;
        values["applicationId"] = integrationEvent.ApplicationId;
        values["submittedAt"] = integrationEvent.SubmittedAt;

        yield return Trigger(
            UserId.From(integrationEvent.ApplicantUserId),
            "candidate",
            integrationEvent.SubmittedAt,
            values,
            subjectType: "application",
            subjectId: integrationEvent.ApplicationId.ToString());

        // Null on an event published before B13.1, and null when Jobs could not read the employer
        // back. Either way there is nobody to tell, which is a quiet outcome rather than a failure.
        if (integrationEvent.EmployerUserId is not { } employerUserId)
        {
            yield break;
        }

        yield return Trigger(
            UserId.From(employerUserId),
            "employer",
            integrationEvent.SubmittedAt,
            values,
            subjectType: "job",
            subjectId: integrationEvent.JobId.ToString());
    }
}
