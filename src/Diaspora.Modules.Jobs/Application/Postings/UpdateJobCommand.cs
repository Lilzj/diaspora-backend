using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>Updates a posting's descriptive fields.</summary>
[RequirePermission(JobPermissions.PostEditOwn)]
[Auditable]
public sealed record UpdateJobCommand : ICommand<JobResponse>
{
    public required Guid JobId { get; init; }

    public required UpdateJobRequest Request { get; init; }
}
