using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects a completed authentication onto the wire. This is the only place the
/// plaintext refresh token is copied out of the descriptor.
/// </summary>
public static class TokenMapper
{
    public const string BearerTokenType = "Bearer";

    public static TokenPairResponse ToResponse(
        AccessTokenDescriptor accessToken,
        RefreshTokenDescriptor refreshToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        ArgumentNullException.ThrowIfNull(refreshToken);

        return new TokenPairResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken.Token,
            TokenType = BearerTokenType,
            ExpiresIn = accessToken.ExpiresInSeconds,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt
        };
    }

    public static AuthenticationResponse ToAuthenticationResponse(
        AuthenticationOutcome outcome,
        Domain.Identity.Users.User user,
        Abstractions.IAvatarUrlResolver? avatars = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new AuthenticationResponse
        {
            Tokens = ToResponse(outcome.AccessToken, outcome.RefreshToken),
            User = UserMapper.ToProfileResponse(user, avatars),
            Access = AccessDecisionMapper.ToResponse(outcome.Access),
            SessionId = outcome.Session.Id.Value
        };
    }
}
