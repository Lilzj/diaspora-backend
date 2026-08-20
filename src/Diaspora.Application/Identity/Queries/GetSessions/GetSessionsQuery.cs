using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Queries.GetSessions;

/// <summary>
/// Lists the member's sessions. Defaults to active only; the full history is available
/// on request so someone investigating suspicious activity can see revoked sessions too.
/// </summary>
[Authorize(Permission = "identity:sessions:read")]
public sealed record GetSessionsQuery : IQuery<IReadOnlyList<SessionResponse>>
{
    public bool IncludeRevoked { get; init; }
}
