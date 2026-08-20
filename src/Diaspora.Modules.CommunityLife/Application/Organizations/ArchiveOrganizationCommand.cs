using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Withdraws an organization page. Reversible only through a restore into draft.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationArchive)]
[Auditable]
public sealed record ArchiveOrganizationCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required ArchiveOrganizationRequest Request { get; init; }
}
