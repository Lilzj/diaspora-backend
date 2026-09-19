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
/// </summary>
[Authorize(Permission = IdentityPermissions.RevokeAnySession)]
public sealed record RevokeMemberSessionsCommand : ICommand<OperationAcknowledgedResponse>
{
    public required Guid UserId { get; init; }

    /// <summary>Why. Recorded in the member's security history.</summary>
    public string? Reason { get; init; }
}
