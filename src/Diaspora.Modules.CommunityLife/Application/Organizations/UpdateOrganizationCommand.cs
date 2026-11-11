using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Edits an organization page.
/// <para>
/// Carries an optional base revision. A client that sends one is refused if somebody else edited
/// first, which is what stops two administrators of the same body silently overwriting each other.
/// </para>
/// </summary>
[RequirePermission(CommunityLifePermissions.OrganizationEditOwn)]
[Auditable]
public sealed record UpdateOrganizationCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required UpdateOrganizationRequest Request { get; init; }
}
