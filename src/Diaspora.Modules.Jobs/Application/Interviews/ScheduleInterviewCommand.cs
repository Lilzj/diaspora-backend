using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>
/// Schedules an interview round against an application.
/// <para>
/// <c>[Transactional]</c>: the handler writes the interview, moves the application and appends to
/// its history. An interview whose application never learned about it would leave the candidate
/// with an appointment and a status that says nobody has looked at them.
/// </para>
/// </summary>
[RequirePermission(JobPermissions.InterviewsSchedule)]
[Auditable]
[Transactional(Reason = "Writes the interview and moves the application it belongs to.")]
public sealed record ScheduleInterviewCommand : ICommand<InterviewResponse>
{
    public required Guid ApplicationId { get; init; }

    public required ScheduleInterviewRequest Request { get; init; }
}
