using Diaspora.Domain.Identity.Sessions;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// The complete result of authenticating: the session, the token pair and where to send
/// the member next.
/// </summary>
public sealed record AuthenticationOutcome
{
    public required UserSession Session { get; init; }

    public required AccessTokenDescriptor AccessToken { get; init; }

    public required RefreshTokenDescriptor RefreshToken { get; init; }

    public required AccessDecision Access { get; init; }
}
