using Diaspora.Domain.Identity.ValueObjects;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// A newly generated refresh token, in both the form the client receives and the form
/// the database stores.
/// <para>
/// <see cref="Token"/> is the only plaintext copy that will ever exist. It must reach the
/// HTTP response and go no further — never a log, never a domain event.
/// </para>
/// </summary>
public sealed record RefreshTokenDescriptor
{
    public required string Token { get; init; }

    public required SecretHash Hash { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}
