using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// Mints and redeems the token in an email footer — B13.3.
/// <para>
/// <b>An unsubscribe link has to work with no session, which is the whole point of it.</b> A member
/// who has stopped opening the app is exactly the member who wants out, and a link that demanded a
/// sign-in first would be a link that does not work for the people it exists for. That makes this the
/// one credential on the platform that arrives with nothing behind it, and it is why it carries so
/// little authority: it names one member and one category, it expires, and the only thing it can do
/// is switch email off.
/// </para>
/// <para>
/// <b>The signature covers every field, including the expiry.</b> Signing the subject and not the
/// deadline is the classic version of this bug and yields a link that never stops working. HMAC-SHA256
/// over a canonical string with a separator that cannot appear in any field, compared in constant
/// time — the same construction <c>SignedUrlFactory</c> uses for a private asset, for the same
/// reasons.
/// </para>
/// <para>
/// <b>It cannot be used to read anything.</b> Redeeming it yields a member and a category and no
/// session, no token and no access to an inbox. Somebody who intercepts one can stop that member's
/// email for one category, which is a real harm and a bounded one; the alternative — a link that
/// signs the reader in — is the design that turns a forwarded email into an account takeover.
/// </para>
/// </summary>
public sealed class UnsubscribeTokenFactory
{
    /// <summary>
    /// Separates the fields inside the signed payload.
    /// <para>
    /// A character that cannot appear in a GUID, a category key or a number, so no combination of
    /// values can be re-partitioned into a different meaning — the canonicalisation bug that makes
    /// two distinct tokens produce one signature.
    /// </para>
    /// </summary>
    private const char FieldSeparator = '|';

    /// <summary>
    /// The key used when a deployment has configured none. One per process, and it must be.
    /// <para>
    /// <b>This is static because the factory is scoped, and getting that wrong makes the feature not
    /// work at all rather than work imperfectly.</b> A per-instance fallback would mint a token
    /// against one request's key and verify it against the next request's — so every unsubscribe link
    /// would be refused as a forgery, in the same process, seconds apart. The documented behaviour
    /// with no key configured is "works, does not survive a restart, not shared between instances",
    /// and only a process-wide value makes that sentence true.
    /// </para>
    /// </summary>
    private static readonly byte[] EphemeralKey = RandomNumberGenerator.GetBytes(32);

    private readonly byte[] _key;
    private readonly NotificationOptions _options;
    private readonly IClock _clock;

    public UnsubscribeTokenFactory(
        IOptions<NotificationOptions> options,
        IClock clock,
        ILogger<UnsubscribeTokenFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _clock = clock;

        if (string.IsNullOrWhiteSpace(_options.UnsubscribeSigningKey))
        {
            // An ephemeral key works and does not survive a restart, and is not shared between
            // instances — so a link minted by one pod is refused by another and every link breaks on
            // deploy. Logged rather than discovered from a support ticket about unsubscribe links
            // that "sometimes say expired".
            logger.LogWarning(
                "No unsubscribe signing key is configured. Links are signed with a key generated at "
                + "startup: they will stop working on restart and will not verify across instances.");

            _key = EphemeralKey;
        }
        else
        {
            _key = Encoding.UTF8.GetBytes(_options.UnsubscribeSigningKey);
        }
    }

    /// <summary>Whether a configured key is in use. Reported by the health contributor.</summary>
    public bool HasConfiguredKey => !string.IsNullOrWhiteSpace(_options.UnsubscribeSigningKey);

    /// <summary>
    /// A token for one member and one category.
    /// <para>
    /// Base64url so it survives a URL, a mail client's rewriting and a copy-paste out of a plain-text
    /// footer without needing escaping anywhere.
    /// </para>
    /// </summary>
    public string Create(UserId userId, string categoryKey)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryKey);

        var expiresAt = _clock.UtcNow
            .AddDays(Math.Max(1, _options.UnsubscribeLinkLifetimeDays))
            .ToUnixTimeSeconds();

        var payload = Canonical(userId.Value, Normalise(categoryKey), expiresAt);

        return Encode($"{payload}{FieldSeparator}{Sign(payload)}");
    }

    /// <summary>
    /// The member and category a token names, or null when it does not verify.
    /// <para>
    /// <b>Every failure returns the same null.</b> Expired, forged, truncated, edited and
    /// well-formed-but-for-a-different-deployment are one answer, because the caller holding this
    /// link is not necessarily the member and telling them which part was wrong is telling them how
    /// to make the next one right.
    /// </para>
    /// </summary>
    public UnsubscribeClaim? Redeem(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
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

        var parts = decoded.Split(FieldSeparator);

        if (parts.Length != 4
            || !Guid.TryParse(parts[0], out var userId)
            || userId == Guid.Empty
            || string.IsNullOrWhiteSpace(parts[1])
            || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiry))
        {
            return null;
        }

        var expected = Sign(Canonical(userId, parts[1], expiry));

        // Constant time. A short-circuiting comparison over a signature is a timing oracle that lets
        // a forger recover it a byte at a time, and this endpoint is unauthenticated.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[3])))
        {
            return null;
        }

        // Checked after the signature, deliberately: an unsigned token's expiry is whatever the
        // holder wrote, so reading it first would be trusting the thing being verified.
        return DateTimeOffset.FromUnixTimeSeconds(expiry) <= _clock.UtcNow
            ? null
            : new UnsubscribeClaim(UserId.From(userId), parts[1]);
    }

    private static string Canonical(Guid userId, string categoryKey, long expiresAt) =>
        string.Join(
            FieldSeparator,
            userId.ToString("D", CultureInfo.InvariantCulture),
            categoryKey,
            expiresAt.ToString(CultureInfo.InvariantCulture));

    private static string Normalise(string categoryKey) => categoryKey.Trim().ToLowerInvariant();

    private static string Encode(string value) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private string Sign(string payload) =>
        Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
}

/// <summary>What a verified unsubscribe token says. A member and a category, and nothing else.</summary>
public sealed record UnsubscribeClaim(UserId UserId, string CategoryKey);
