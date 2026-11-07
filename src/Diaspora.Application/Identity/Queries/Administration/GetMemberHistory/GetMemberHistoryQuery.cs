using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.Administration.GetMemberHistory;

/// <summary>
/// A member's security history, for staff answering a question about their account.
/// <para>
/// The same records the member can read about themselves, read by somebody else — sign-ins
/// and the devices they came from, failed attempts, password changes, suspensions. It is
/// behind its own permission rather than folded into reading the account, because "look up
/// who this is" and "read a log of everything they have done" are different asks, and a
/// moderator confirming an identity needs the first without the second.
/// </para>
/// </summary>
[Auditable(Action = "identity.member.security-events.read", OnlyForStaff = true)]
[Authorize(Permission = IdentityPermissions.ReadAnySecurityEvents)]
public sealed record GetMemberHistoryQuery : IQuery<IReadOnlyList<SecurityEventResponse>>
{
    public required Guid UserId { get; init; }

    public int Limit { get; init; } = 50;
}
