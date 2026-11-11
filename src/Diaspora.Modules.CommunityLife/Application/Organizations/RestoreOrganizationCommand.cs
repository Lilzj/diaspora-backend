using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Brings an archived or deleted organization back, as a draft.
/// <para>
/// Never straight to published: a page that has been away wants its contact details, its opening
/// hours and its leadership looked at before members are pointed at it again.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.OrganizationRestore)]
[Auditable]
public sealed record RestoreOrganizationCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }
}
