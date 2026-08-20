using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Replaces the leadership list shown on an organization page.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationEditOwn)]
[Auditable]
public sealed record SetOrganizationLeadersCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SetOrganizationLeadersRequest Request { get; init; }
}
