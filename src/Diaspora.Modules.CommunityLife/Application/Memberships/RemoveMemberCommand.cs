using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>Removes somebody from an organization.</summary>
[RequirePermission(CommunityLifePermissions.MembershipManage)]
[Auditable]
public sealed record RemoveMemberCommand : ICommand<MembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required Guid MembershipId { get; init; }

    public required RemoveMemberRequest Request { get; init; }
}
