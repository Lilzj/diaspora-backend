using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.Logout;

/// <summary>
/// Ends the caller's own session. The session is taken from the presented access token,
/// never from the request body, so this endpoint cannot sign anyone else out.
/// </summary>
[Auditable(Action = "identity.user.logged-out")]
public sealed record LogoutCommand : ICommand<OperationAcknowledgedResponse>
{
    /// <summary>When true, every other session on the account ends as well.</summary>
    public bool RevokeAllSessions { get; init; }
}
