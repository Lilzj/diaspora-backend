using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.ValueObjects;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Generates opaque refresh tokens and hashes presented ones for lookup.
/// <para>
/// Refresh tokens are random secrets, not JWTs: they need to be revocable and
/// individually traceable, and a self-describing token cannot be either.
/// </para>
/// </summary>
public interface IRefreshTokenIssuer
{
    RefreshTokenDescriptor Issue(DateTimeOffset now);

    /// <summary>
    /// Hashes a token presented by a client so it can be matched against storage.
    /// Must be deterministic — this is a lookup key, not a password hash.
    /// </summary>
    SecretHash ComputeHash(string token);
}
