namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// A freshly issued access/refresh token pair.
/// <para>
/// The refresh token is returned in plaintext exactly once, here. Only its hash is
/// stored, so it cannot be recovered later — a client that loses it must sign in again.
/// </para>
/// </summary>
public sealed record TokenPairResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required string TokenType { get; init; }

    /// <summary>Seconds until the access token expires.</summary>
    public required int ExpiresIn { get; init; }

    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    public required DateTimeOffset RefreshTokenExpiresAt { get; init; }
}
