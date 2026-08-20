using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Password strength, history and expiry rules.
/// </summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "Identity:Password";

    /// <summary>
    /// Length floor. Twelve rather than the more common eight: length is the single
    /// most effective defence against offline cracking, and it costs the member nothing
    /// they cannot get from a passphrase.
    /// </summary>
    [Range(8, 64)]
    public int MinimumLength { get; set; } = 12;

    /// <summary>
    /// Upper bound. Exists to stop a multi-megabyte input from turning the deliberately
    /// slow hash into a denial-of-service vector, not to constrain the member.
    /// </summary>
    [Range(64, 512)]
    public int MaximumLength { get; set; } = 128;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Off by default. Symbol requirements push people toward predictable substitutions
    /// far more reliably than they add entropy; the length floor does the real work.
    /// </summary>
    public bool RequireNonAlphanumeric { get; set; }

    /// <summary>Refuses passwords containing the member's own name or phone number.</summary>
    public bool ForbidPersonalInformation { get; set; } = true;

    /// <summary>How many retired hashes to keep and refuse.</summary>
    [Range(0, 24)]
    public int HistoryDepth { get; set; } = 5;

    public bool EnforceHistory { get; set; } = true;

    /// <summary>
    /// Password expiry in days. Zero disables it, which is the default — forced rotation
    /// is now understood to drive weaker passwords, so the hook exists for compliance
    /// regimes that still demand it rather than because it helps.
    /// </summary>
    [Range(0, 3650)]
    public int MaximumAgeDays { get; set; }

    /// <summary>Iteration count for the PBKDF2 hasher.</summary>
    [Range(10_000, 1_000_000)]
    public int HashIterations { get; set; } = 210_000;
}
