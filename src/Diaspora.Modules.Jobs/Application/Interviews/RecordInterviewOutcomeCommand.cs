using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Interviews;

/// <summary>Records what an interview concluded.</summary>
[Authorize(Permission = JobPermissions.InterviewsRecord)]
[Auditable]
[Transactional(Reason = "Records the outcome and appends it to the application's history.")]
public sealed record RecordInterviewOutcomeCommand : ICommand<InterviewResponse>
{
    public required Guid InterviewId { get; init; }

    public required RecordInterviewOutcomeRequest Request { get; init; }
}
