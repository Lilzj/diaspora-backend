using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>What an administrator sees when they open their organization.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationEditOwn)]
public sealed record GetOrganizationDashboardQuery : IQuery<OrganizationDashboardResponse>
{
    public required Guid OrganizationId { get; init; }
}
