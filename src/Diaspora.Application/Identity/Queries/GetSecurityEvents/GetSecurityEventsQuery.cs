using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetSecurityEvents;

/// <summary>
/// Returns the signed-in member's recent security history — sign-ins, password changes,
/// revoked sessions. The member is the first line of defence for their own account, and
/// they cannot notice anything they cannot see.
/// </summary>
[RequirePermission("identity:security-events:read")]
public sealed record GetSecurityEventsQuery : IQuery<IReadOnlyList<SecurityEventResponse>>
{
    public int Limit { get; init; } = 50;
}
