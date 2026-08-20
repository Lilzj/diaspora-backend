using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>Invites somebody into an organization.</summary>
[Authorize(Permission = CommunityLifePermissions.MembershipManage)]
[Auditable]
public sealed record InviteMemberCommand : ICommand<MembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required InviteMemberRequest Request { get; init; }
}
