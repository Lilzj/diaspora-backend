using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>
/// Approves or refuses a pending membership.
/// <para>
/// Serves both directions: an administrator deciding a request, and a member accepting or declining
/// an invitation. Which one the caller is doing follows from what is pending, so a client needs one
/// endpoint rather than four.
/// </para>
/// </summary>
[RequireAuthentication(
    "Authentication is all the gate can ask: this serves an administrator deciding a request and " +
    "a member accepting an invitation, and which one the caller is doing follows from what is " +
    "pending. The handler reads that.")]
[Auditable]
public sealed record DecideMembershipCommand : ICommand<MembershipResponse>
{
    public required Guid OrganizationId { get; init; }

    public required Guid MembershipId { get; init; }

    public required DecideMembershipRequest Request { get; init; }
}
