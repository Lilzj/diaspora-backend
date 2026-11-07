using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Administration.RevokeMemberSessions;

/// <summary>
/// Signs a member out everywhere.
/// <para>
/// What staff reach for when an account may be in somebody else's hands: the member keeps
/// their account and their password, and every device holding a live session stops being
/// able to use it. It is deliberately not a suspension — the member can sign back in — so
/// it is the safe move while a report is still being looked at.
/// </para>
/// <para>
/// Since 7 Nov 2026 it needs a reason, is recorded in the audit trail, and is refused on the
/// caller's own account or one at or above their rank.
/// </para>
/// </summary>
[Auditable(Action = "identity.user.sessions-revoked")]
[Authorize(Permission = IdentityPermissions.RevokeAnySession)]
public sealed record RevokeMemberSessionsCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>Why. Required; recorded in the member's security history.</summary>
    public string? Reason { get; init; }
}
