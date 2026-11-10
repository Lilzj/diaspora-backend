using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Puts an organization on the front of the directory, or takes it off.
/// <para>
/// Staff only. Featuring is editorial, and a body that could feature itself would make the shelf
/// worthless within a week.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.OrganizationFeature)]
[Auditable]
public sealed record SetOrganizationFeaturedCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SetFeaturedRequest Request { get; init; }
}
