using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.Logout;

/// <summary>
/// Ends the caller's own session. The session is taken from the presented access token,
/// never from the request body, so this endpoint cannot sign anyone else out.
/// </summary>
[Auditable(Action = "identity.user.logged-out")]
// Ending the caller's own session. The route is [Authorize] with no policy and that is right:
// there is no permission a member could lack that should keep them signed in.
[RequireAuthentication(
    "Ending the caller's own session. There is no permission a member could lack that should keep " +
    "them signed in.")]
public sealed record LogoutCommand : ICommand<OperationAcknowledgedResponse>
{
    /// <summary>When true, every other session on the account ends as well.</summary>
    public bool RevokeAllSessions { get; init; }
}
