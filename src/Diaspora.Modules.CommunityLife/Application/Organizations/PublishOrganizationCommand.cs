using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Makes an organization discoverable, or submits it for review first.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationPublish)]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Events)]
public sealed record PublishOrganizationCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required PublishOrganizationRequest Request { get; init; }
}
