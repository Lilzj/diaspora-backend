using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Removes an asset from an organization page.</summary>
[RequirePermission(CommunityLifePermissions.OrganizationManageMedia)]
[Auditable]
public sealed record DetachOrganizationMediaCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required Guid MediaId { get; init; }
}
