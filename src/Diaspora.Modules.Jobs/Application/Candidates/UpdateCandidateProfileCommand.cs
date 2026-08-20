using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>Updates the caller's own candidate profile.</summary>
[Authorize(Permission = JobPermissions.CandidateManage)]
[Auditable]
public sealed record UpdateCandidateProfileCommand : ICommand<CandidateProfileResponse>
{
    public required UpdateCandidateProfileRequest Request { get; init; }
}
