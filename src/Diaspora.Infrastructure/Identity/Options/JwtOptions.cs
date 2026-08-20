using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Signing and validation parameters for access tokens.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Identity:Jwt";

    [Required]
    public string Issuer { get; set; } = "https://identity.diaspora.app";

    [Required]
    public string Audience { get; set; } = "https://api.diaspora.app";

    /// <summary>
    /// HMAC signing key. Must come from a secret store in any deployed environment —
    /// the startup validator refuses to run outside Development without one.
    /// <para>
    /// In Development a blank value is replaced by <see cref="DevelopmentSigningKey"/> by
    /// <see cref="JwtDevelopmentSigningKeyResolver"/>, so every consumer — the access token
    /// issuer, the service token issuer, the bearer validator and the OTP code generator —
    /// reads one resolved value. Nothing else may substitute a fallback.
    /// </para>
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// The signing key a Development host uses when configuration supplies none, so a fresh
    /// clone runs without a secret store.
    /// <para>
    /// <b>It lives here rather than in the API layer because both halves of the feature need
    /// it.</b> While it lived beside the bearer configurator only the validating half could
    /// see it, and the issuing half — which lives in this assembly and cannot reference the
    /// API — threw on the empty string instead.
    /// </para>
    /// <para>
    /// It is not a secret and must never be treated as one: it is committed, identical on
    /// every machine, and refused outside Development by <see cref="JwtOptionsValidator"/>.
    /// </para>
    /// </summary>
    public const string DevelopmentSigningKey =
        "diaspora-development-signing-key-not-for-any-deployed-environment";

    /// <summary>
    /// Minimum key length in bytes. 32 is the floor for HMAC-SHA256; a shorter key
    /// weakens the signature regardless of how random it looks.
    /// </summary>
    public const int MinimumSigningKeyBytes = 32;

    /// <summary>
    /// Access token lifetime. Deliberately short: the token carries a permissions
    /// snapshot, and the window during which that snapshot can be stale is exactly
    /// this value.
    /// </summary>
    [Range(1, 60)]
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>
    /// Tolerance for clock drift between the issuing and validating hosts. Kept small;
    /// the default five minutes materially extends the life of an expired token.
    /// </summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to embed the feature-gate snapshot in the token. Off by default — it is
    /// available from <c>GET /access/features</c> and would otherwise ride on every request.
    /// </summary>
    public bool IncludeFeatureClaims { get; set; }
}
