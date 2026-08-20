using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Errors;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// Mints and redeems the short-lived tokens that carry access to a private asset.
/// <para>
/// <b>Why the platform signs its own tokens rather than always delegating to the provider.</b>
/// A provider-presigned URL is excellent when the provider supports it, and Media uses one
/// when it can. But the filesystem provider cannot presign anything, and more importantly a
/// provider-signed URL bypasses the platform entirely — no access check, no audit line, no
/// revocation. For a verification document that is the wrong trade. This token is redeemed at
/// a platform endpoint, so the read is still authorised, still logged, and still stops working
/// the moment the asset is quarantined.
/// </para>
/// <para>
/// <b>The token binds four things:</b> the asset, the variant, the audience, and the expiry.
/// Binding the audience is what stops a link shared in a support ticket from working for the
/// person it was shared with — the token names who may redeem it, and a different caller
/// presenting it is refused. Binding the variant stops a link to a thumbnail being replayed
/// for the full-size original, which for an immigration document is the whole ballgame.
/// </para>
/// <para>
/// HMAC-SHA256 over a canonical string, compared in constant time. The signature covers every
/// field, so nothing in the token can be edited without invalidating it — the classic mistake
/// being to sign the id and not the expiry, which yields a link that never expires.
/// </para>
/// </summary>
public sealed class SignedUrlFactory
{
    /// <summary>
    /// Separates the fields inside the signed payload. A character that cannot appear in any
    /// field, so no combination of values can be re-partitioned into a different meaning —
    /// the canonicalisation bug that makes two distinct tokens produce one signature.
    /// </summary>
    private const char FieldSeparator = '|';

    private readonly byte[] _key;
    private readonly MediaSecurityOptions _options;
    private readonly IClock _clock;

    public SignedUrlFactory(IOptions<MediaSecurityOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _clock = clock;

        _key = string.IsNullOrWhiteSpace(_options.SigningKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(_options.SigningKey);
    }

    /// <summary>
    /// Whether a configured key is in use.
    /// <para>
    /// False means the module generated an ephemeral key at startup, which works but does not
    /// survive a restart and is not shared between instances. Reported by the readiness check,
    /// because "signed links stop working when a pod recycles" is otherwise a maddening
    /// intermittent bug.
    /// </para>
    /// </summary>
    public bool HasConfiguredKey => !string.IsNullOrWhiteSpace(_options.SigningKey);

    /// <summary>Mints a token for one asset, one variant, one caller, for a bounded time.</summary>
    public SignedMediaToken Create(
        MediaId mediaId,
        MediaVariantPurpose purpose,
        UserId? audience,
        MediaKind kind,
        TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        var window = lifetime ?? DefaultLifetimeFor(kind);
        var expiresAt = _clock.UtcNow.Add(window);

        var payload = Canonicalize(mediaId, purpose, audience, expiresAt);
        var signature = Sign(payload);

        MediaTelemetry.SignedUrlsIssued.Add(1,
            new KeyValuePair<string, object?>("kind", kind.ToString()));

        return new SignedMediaToken
        {
            MediaId = mediaId,
            Purpose = purpose,
            Audience = audience,
            ExpiresAt = expiresAt,
            Signature = signature
        };
    }

    /// <summary>
    /// Checks a presented token.
    /// <para>
    /// Signature first, then expiry, then audience. The order matters for what an attacker
    /// learns: verifying the signature before anything else means a forged token produces one
    /// answer regardless of what it claims, rather than leaking whether the asset exists or
    /// whether the expiry was the only thing wrong.
    /// </para>
    /// </summary>
    public Result Validate(SignedMediaToken token, UserId? caller)
    {
        ArgumentNullException.ThrowIfNull(token);

        var expected = Sign(Canonicalize(token.MediaId, token.Purpose, token.Audience, token.ExpiresAt));

        if (!FixedTimeEquals(expected, token.Signature))
        {
            MediaTelemetry.SignedUrlsRejected.Add(1,
                new KeyValuePair<string, object?>("reason", "invalid"));

            return Result.Failure(MediaErrors.SignedUrlInvalid);
        }

        if (token.ExpiresAt <= _clock.UtcNow)
        {
            MediaTelemetry.SignedUrlsRejected.Add(1,
                new KeyValuePair<string, object?>("reason", "expired"));

            return Result.Failure(MediaErrors.SignedUrlExpired);
        }

        // A token minted for a specific member is only usable by that member. Without this, a
        // link pasted into a support ticket works for whoever reads the ticket.
        if (token.Audience is not null && (caller is null || caller != token.Audience))
        {
            MediaTelemetry.SignedUrlsRejected.Add(1,
                new KeyValuePair<string, object?>("reason", "audience"));

            return Result.Failure(MediaErrors.SignedUrlInvalid);
        }

        return Result.Success();
    }

    private TimeSpan DefaultLifetimeFor(MediaKind kind) =>
        kind is MediaKind.VerificationDocument
            ? TimeSpan.FromMinutes(_options.SensitiveSignedUrlMinutes)
            : TimeSpan.FromMinutes(_options.SignedUrlMinutes);

    /// <summary>
    /// The exact bytes that get signed. Every field is present, in a fixed order, with a
    /// separator that cannot occur inside a field — so no two distinct tokens can canonicalise
    /// to the same string.
    /// </summary>
    private static string Canonicalize(
        MediaId mediaId, MediaVariantPurpose purpose, UserId? audience, DateTimeOffset expiresAt) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{mediaId.Value:N}{FieldSeparator}" +
            $"{(int)purpose}{FieldSeparator}" +
            $"{audience?.Value.ToString("N", CultureInfo.InvariantCulture) ?? string.Empty}{FieldSeparator}" +
            $"{expiresAt.ToUnixTimeSeconds()}");

    private string Sign(string payload) =>
        Base64UrlEncode(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

    private static bool FixedTimeEquals(string expected, string presented)
    {
        if (string.IsNullOrEmpty(presented) || expected.Length != presented.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(presented));
    }

    /// <summary>
    /// Base64 without the three characters that need escaping in a URL. A signature that has
    /// to be percent-encoded is a signature that arrives differently depending on which client
    /// library sent it.
    /// </summary>
    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
