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
/// HMAC-signed email change links, built as <see cref="EmailVerificationLinks"/> is.
/// <para>
/// <b>Its own derived key.</b> HKDF over the JWT signing key with a different purpose label, so a
/// verification link can never be presented as a change link or the other way round, even though
/// both are carried by the same kind of email.
/// </para>
/// <para>
/// <b>Shorter-lived than a verification link.</b> A verification link proves something about an
/// address already on the account; this one moves the account. Two hours is long enough to open an
/// inbox and short enough that a link sitting in a forwarded or compromised mailbox goes stale.
/// </para>
/// </summary>
public sealed class EmailChangeLinks : IEmailChangeLinks
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    // Neither an address (EmailAddress allows only [a-z0-9._%+-@]) nor a number contains it.
    private const char Separator = '|';
    private const int FieldCount = 7;
    private const string NoSession = "-";
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("diaspora:email-change:v1");

    private readonly byte[] _key;
    private readonly IClock _clock;

    public EmailChangeLinks(IOptions<JwtOptions> jwt, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(jwt);

        var secret = jwt.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("Identity:Jwt:SigningKey is required to sign email change links.");
        }

        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(secret), 32, salt: [], info: Purpose);
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAt) Create(EmailChangeClaim request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FromAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ToAddress);

        var expiresAt = _clock.UtcNow.Add(Lifetime);
        var payload = Canonical(
            request.UserId.Value,
            Normalise(request.FromAddress),
            Normalise(request.ToAddress),
            request.SecurityStamp,
            request.RequestedBySession?.Value,
            expiresAt.ToUnixTimeSeconds());

        return (Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{payload}{Separator}{Sign(payload)}")), expiresAt);
    }

    public EmailChangeClaim? Redeem(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
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

        // user | from | to | stamp | session | expiry | signature. From may be empty (an account
        // that had no address); to may not.
        var parts = decoded.Split(Separator);

        if (parts.Length != FieldCount
            || !Guid.TryParse(parts[0], out var userId)
            || userId == Guid.Empty
            || string.IsNullOrWhiteSpace(parts[2])
            || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var stamp)
            || !TryReadSession(parts[4], out var session)
            || !long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry))
        {
            return null;
        }

        var expected = Sign(Canonical(userId, parts[1], parts[2], stamp, session, expiry));

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[6])))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(expiry) <= _clock.UtcNow
            ? null
            : new EmailChangeClaim(
                UserId.From(userId),
                parts[1],
                parts[2],
                stamp,
                session is { } id ? SessionId.From(id) : null);
    }

    private static bool TryReadSession(string field, out Guid? session)
    {
        session = null;

        if (field == NoSession)
        {
            return true;
        }

        if (Guid.TryParse(field, out var id) && id != Guid.Empty)
        {
            session = id;
            return true;
        }

        return false;
    }

    private static string Normalise(string address) => address.Trim().ToLowerInvariant();

    private static string Canonical(Guid userId, string from, string to, int stamp, Guid? session, long expiresAt) =>
        string.Join(
            Separator,
            userId.ToString("D", CultureInfo.InvariantCulture),
            from,
            to,
            stamp.ToString(CultureInfo.InvariantCulture),
            session?.ToString("D", CultureInfo.InvariantCulture) ?? NoSession,
            expiresAt.ToString(CultureInfo.InvariantCulture));

    private string Sign(string payload) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
}

/// <summary>
/// The default: no email transport, so no change can be requested. The email adapter replaces it
/// on last-registration-wins once a provider is configured.
/// </summary>
public sealed class UnconfiguredEmailChangeMailer : IEmailChangeMailer
{
    public bool IsConfigured => false;

    public Task<bool> SendConfirmationAsync(EmailChangeLinkMessage message, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SendAddressUnavailableAsync(
        string address, string? language, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SendNoticeAsync(EmailChangeNotice notice, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
