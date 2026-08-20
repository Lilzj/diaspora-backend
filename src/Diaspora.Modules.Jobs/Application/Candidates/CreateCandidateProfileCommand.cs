using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;

namespace Diaspora.Modules.Jobs.Application.Candidates;

/// <summary>
/// Creates the caller's candidate profile.
/// <para>
/// Gated at Limited, not Verified: building a profile is a private, low-stakes act and exactly
/// what a new member should be doing. It is <em>applying</em> that puts a real name in front of a
/// stranger, and that is where the Verified floor bites.
/// </para>
/// </summary>
[Authorize(Permission = JobPermissions.CandidateManage)]
[Auditable]
public sealed record CreateCandidateProfileCommand : ICommand<CandidateProfileResponse>
{
    public required CreateCandidateProfileRequest Request { get; init; }
}
