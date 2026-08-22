using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Replaces the guides an organization points its readers at.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationEditOwn)]
[Auditable]
public sealed record SetOrganizationGuidesCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SetOrganizationGuidesRequest Request { get; init; }
}
