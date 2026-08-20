using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Authentication.RefreshToken;

/// <summary>
/// Rotates a refresh token for a new token pair. Unauthenticated by design — the refresh
/// token is itself the credential, and the access token it renews has usually expired.
/// </summary>
[Auditable(Action = "identity.user.token-refreshed")]
public sealed record RefreshTokenCommand : ICommand<AuthenticationResponse>
{
    public required string RefreshToken { get; init; }
}
