using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Asks to join an organization, or joins it outright.
/// <para>
/// Which of the two happens is the body's membership policy to decide, not the caller's — an open
/// association admits, one that vets queues.
/// </para>
/// </summary>
[Authorize(Permission = CommunityLifePermissions.MembershipJoin)]
[Auditable]
public sealed record JoinOrganizationCommand : ICommand<MembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required JoinOrganizationRequest Request { get; init; }
}
