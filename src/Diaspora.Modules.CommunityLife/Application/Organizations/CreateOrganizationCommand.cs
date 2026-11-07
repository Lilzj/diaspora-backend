using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Drafts an organization.
/// <para>
/// Transactional: the organization, its owner membership and its analytics record commit together.
/// An organization whose owner membership failed to save would be a page nobody could administer.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationCreate)]
[Transactional(Reason = "Writes the organization, its owner membership and its analytics record.")]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Events)]
public sealed record CreateOrganizationCommand : ICommand<OrganizationResponse>
{
    public required CreateOrganizationRequest Request { get; init; }
}
