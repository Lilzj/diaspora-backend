using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Administration.AssignRole;

/// <summary>
/// Gives a member a platform role, or takes one away, at once.
/// <para>
/// <b>The aggregate has had <c>AssignRole</c> since the beginning and nothing ever called
/// it.</b> Every staff account on this platform was made with a database write, which is
/// the sort of thing that is fine once and is not a way to run a team.
/// </para>
/// <para>
/// Since 7 Nov 2026: a reason is required, it is recorded and the member is told, and the caller
/// must be above the account in rank. Granting Admin or SuperAdmin is refused here — it needs a
/// second member of staff, through <c>ProposeRoleGrantCommand</c>.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.role-changed")]
[Authorize(Permission = IdentityPermissions.AssignRoles)]
public sealed record AssignRoleCommand : ICommand<AdminMemberResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>A <c>UserRole</c> name: <c>CommunityUser</c>, <c>Moderator</c>, <c>Admin</c>…</summary>
    public required string Role { get; init; }

    /// <summary>Why. Required; recorded in the member's security history and told to the member.</summary>
    public string? Reason { get; init; }
}
