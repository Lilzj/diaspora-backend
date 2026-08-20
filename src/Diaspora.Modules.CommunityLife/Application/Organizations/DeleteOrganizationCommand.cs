using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Soft-deletes an organization page.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationArchive)]
[Auditable]
public sealed record DeleteOrganizationCommand : ICommand
{
    public required Guid OrganizationId { get; init; }
}
