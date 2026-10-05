using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.EmailVerification;

/// <summary>
/// HMAC-signed verification links, after the pattern of <c>UnsubscribeTokenFactory</c>.
/// <para>
/// <b>The key is derived, not configured.</b> HKDF over the JWT signing key with a purpose label
/// gives a key that is as secret as the one already guarding every session, identical on every
/// machine, and stable across restarts, with nothing new for an operator to remember to set. The
/// label keeps it from ever equalling the JWT key itself, so a verification link can never be
/// replayed as anything else. Rotating the JWT key retires outstanding links, which costs a member
/// one "send again".
/// </para>
/// </summary>
public sealed class EmailVerificationLinks : IEmailVerificationLinks
{
    /// <summary>"Hours rather than days", as the verification contract asks.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private const char Separator = '|';
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("diaspora:email-verification:v1");

    private readonly byte[] _key;
    private readonly IClock _clock;

    public EmailVerificationLinks(IOptions<JwtOptions> jwt, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(jwt);

        var secret = jwt.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            // The JWT validator refuses to start a deployment without a key, so this only fires
            // in a host that skipped it. Failing loudly beats signing with an empty key.
            throw new InvalidOperationException("Identity:Jwt:SigningKey is required to sign verification links.");
        }

        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, salt: [], info: Purpose);
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAt) Create(UserId userId, string address)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        var expiresAt = _clock.UtcNow.Add(Lifetime);
        var payload = Canonical(userId.Value, address, expiresAt.ToUnixTimeSeconds());

        return (Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{payload}{Separator}{Sign(payload)}")), expiresAt);
    }

    public EmailVerificationClaim? Redeem(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 1024)
        {
            return null;
        }

        string decoded;

        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
        }
        catch (FormatException)
        {
            return null;
        }

        // An address cannot contain the separator, so exactly four fields or it is not ours.
        var parts = decoded.Split(Separator);

        if (parts.Length != 4
            || !Guid.TryParse(parts[0], out var userId)
            || userId == Guid.Empty
            || string.IsNullOrWhiteSpace(parts[1])
            || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry))
        {
            return null;
        }

        var expected = Sign(Canonical(userId, parts[1], expiry));

        // Constant time: this is read on a public page, and a short-circuiting comparison of a
        // signature lets a forger recover it a byte at a time.
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[3])))
        {
            return null;
        }

        // After the signature, never before: an unsigned expiry is whatever the holder wrote.
        return DateTimeOffset.FromUnixTimeSeconds(expiry) <= _clock.UtcNow
            ? null
            : new EmailVerificationClaim(UserId.From(userId), parts[1]);
    }

    private static string Canonical(Guid userId, string address, long expiresAt) =>
        string.Join(
            Separator,
            userId.ToString("D", CultureInfo.InvariantCulture),
            address.Trim().ToLowerInvariant(),
            expiresAt.ToString(CultureInfo.InvariantCulture));

    private string Sign(string payload) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
}

/// <summary>
/// The default: no email transport, so no link can be sent. The email adapter replaces it on
/// last-registration-wins once a provider is configured.
/// </summary>
public sealed class UnconfiguredEmailVerificationSender : IEmailVerificationSender
{
    public bool IsConfigured => false;

    public Task<bool> SendAsync(EmailVerificationMessage message, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
