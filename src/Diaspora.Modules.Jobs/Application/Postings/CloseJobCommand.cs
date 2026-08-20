using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>
/// Closes a posting to applications.
/// <para>
/// Granted under restriction, because leaving up a vacancy that no longer exists wastes
/// applicants' afternoons rather than the restricted recruiter's.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.PostClose)]
[Auditable]
public sealed record CloseJobCommand : ICommand<JobResponse>
{
    public required Guid JobId { get; init; }

    public required CloseJobRequest Request { get; init; }
}
