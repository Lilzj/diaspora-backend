using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Replaces an organization's opening hours.</summary>
[RequirePermission(CommunityLifePermissions.OrganizationEditOwn)]
[Auditable]
public sealed record SetOperatingHoursCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SetOperatingHoursRequest Request { get; init; }
}
