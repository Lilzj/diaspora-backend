using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Catalog;

/// <summary>Adds an organization type or restates one. Staff only.</summary>
[RequirePermission(CommunityLifePermissions.ManageCatalogue)]
[Auditable]
public sealed record UpsertOrganizationTypeCommand : ICommand<OrganizationTypeResponse>
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? IconName { get; init; }

    public string? GroupName { get; init; }

    public int Order { get; init; }

    public bool RequiresVerification { get; init; }

    public bool? IsActive { get; init; }
}
