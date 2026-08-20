using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>Promotes or stands down a member.</summary>
[Authorize(Permission = CommunityLifePermissions.MembershipManage)]
[Auditable]
public sealed record ChangeMemberRoleCommand : ICommand<MembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required Guid MembershipId { get; init; }

    public required ChangeMemberRoleRequest Request { get; init; }
}
