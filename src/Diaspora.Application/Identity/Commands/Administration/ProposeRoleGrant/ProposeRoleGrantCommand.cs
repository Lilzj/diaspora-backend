using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Administration.ProposeRoleGrant;

/// <summary>
/// Proposes granting Admin or SuperAdmin, which a second member of staff confirms (7 Nov 2026).
/// <para>
/// Those are the roles that close accounts and appoint staff, so one person must not hand them out
/// alone. Granting SuperAdmin keeps its own rule at both ends: only a SuperAdmin proposes it and
/// only a SuperAdmin confirms it.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.role-grant-proposed")]
[Authorize(Permission = IdentityPermissions.AssignRoles)]
public sealed record ProposeRoleGrantCommand : ICommand<AccountActionProposalResponse>
{
    public required Guid UserId { get; init; }

    /// <summary><c>Admin</c> or <c>SuperAdmin</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Why. Told to the member once confirmed.</summary>
    public string? Reason { get; init; }
}
