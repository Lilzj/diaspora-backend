using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>Asks Trust to verify an organization.</summary>
[Authorize(Permission = CommunityLifePermissions.OrganizationSubmitVerification)]
[Auditable]
public sealed record SubmitOrganizationVerificationCommand : ICommand<OrganizationResponse>
{
    public required Guid OrganizationId { get; init; }

    public required SubmitOrganizationVerificationRequest Request { get; init; }
}
