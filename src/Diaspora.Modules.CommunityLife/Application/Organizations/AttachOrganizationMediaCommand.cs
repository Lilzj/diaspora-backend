using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Attaches an uploaded asset to an organization page.</summary>
[RequirePermission(CommunityLifePermissions.OrganizationManageMedia)]
[Auditable]
public sealed record AttachOrganizationMediaCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required MediaAttachmentRequest Request { get; init; }
}
