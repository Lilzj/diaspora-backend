using System.Globalization;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Media.Application.Services;

/// <summary>
/// A minted access token, and the query parameters that carry it.
/// <para>
/// Carried as query parameters rather than as a header, because the whole point is a URL that
/// can be put in an <c>&lt;img src&gt;</c> — and an image tag cannot send a header. That is
/// also why the token binds an audience and a short expiry: a credential in a URL will end up
/// in a browser history, a referrer and a proxy log, and the only defence that survives all
/// three is for the credential to be nearly worthless by the time anyone reads it there.
/// </para>
/// </summary>
public sealed record SignedMediaToken
{
    /// <summary>Query parameter names. Frozen: they appear in URLs clients have already stored.</summary>
    public const string ExpiryParameter = "exp";

    public const string SignatureParameter = "sig";

    public const string AudienceParameter = "aud";

    public const string VariantParameter = "v";

    public required MediaId MediaId { get; init; }

    public required MediaVariantPurpose Purpose { get; init; }

    /// <summary>Who may redeem this. Null means anyone holding the link.</summary>
    public UserId? Audience { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required string Signature { get; init; }

    /// <summary>The query string this token travels in, without a leading question mark.</summary>
    public string ToQueryString()
    {
        var audience = Audience is null
            ? string.Empty
            : $"&{AudienceParameter}={Audience.Value:N}";

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{VariantParameter}={Purpose.ToString().ToLowerInvariant()}" +
            $"&{ExpiryParameter}={ExpiresAt.ToUnixTimeSeconds()}" +
            $"{audience}" +
            $"&{SignatureParameter}={Signature}");
    }
}
