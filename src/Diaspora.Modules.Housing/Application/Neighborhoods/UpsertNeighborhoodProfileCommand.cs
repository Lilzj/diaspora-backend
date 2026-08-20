using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Writes or revises a neighbourhood profile.
/// <para>
/// Writing is not approving. A change lands unapproved and the profile says so until somebody with
/// the approval permission signs it off - which is what makes these summaries safe to let a wide
/// set of contributors edit.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.NeighborhoodManage)]
[Auditable]
public sealed record UpsertNeighborhoodProfileCommand : ICommand<NeighborhoodProfileResponse>
{
    public required UpsertNeighborhoodProfileRequest Request { get; init; }
}
