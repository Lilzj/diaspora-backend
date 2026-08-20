namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Exchanges a refresh token for a new token pair. The presented token is always
/// invalidated, whether or not the exchange succeeds.
/// </summary>
public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}
