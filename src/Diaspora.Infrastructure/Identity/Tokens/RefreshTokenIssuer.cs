using System.Security.Cryptography;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Infrastructure.Identity.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Tokens;

/// <summary>
/// Generates opaque refresh tokens and hashes presented ones for lookup.
/// <para>
/// Refresh tokens are random secrets rather than JWTs. A self-describing token cannot be
/// revoked without a denylist, and revocability is the entire point here: rotation,
/// per-session termination and replay detection all depend on the server being the
/// authority on whether a given token is still live.
/// </para>
/// <para>
/// The lookup hash is a plain SHA-256, deliberately, not a password hash. It has to be
/// deterministic to serve as an index key, and the input already carries 256 bits of
/// entropy — there is nothing for a slow KDF to defend against.
/// </para>
/// </summary>
public sealed class RefreshTokenIssuer : IRefreshTokenIssuer
{
    private readonly RefreshTokenOptions _options;

    public RefreshTokenIssuer(IOptions<RefreshTokenOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public RefreshTokenDescriptor Issue(DateTimeOffset now)
    {
        var bytes = RandomNumberGenerator.GetBytes(_options.TokenSizeBytes);

        // Base64url so the token survives headers, query strings and JSON untouched.
        var token = Base64UrlEncode(bytes);

        return new RefreshTokenDescriptor
        {
            Token = token,
            Hash = ComputeHash(token),
            ExpiresAt = now.AddDays(_options.LifetimeDays)
        };
    }

    public SecretHash ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return SecretHash.Create(Convert.ToHexString(digest));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
