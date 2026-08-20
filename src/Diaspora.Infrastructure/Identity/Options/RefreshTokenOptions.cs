using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Refresh token and session lifetimes.
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "Identity:RefreshToken";

    /// <summary>
    /// How long a single refresh token may be redeemed for. Each rotation issues a fresh
    /// one, so this is the maximum idle period before a member must sign in again.
    /// </summary>
    [Range(1, 365)]
    public int LifetimeDays { get; set; } = 30;

    /// <summary>
    /// The hard ceiling on a session's life. Rotation never extends it, so a session
    /// cannot be kept alive indefinitely by an attacker who keeps refreshing.
    /// </summary>
    [Range(1, 365)]
    public int AbsoluteSessionLifetimeDays { get; set; } = 90;

    /// <summary>Entropy per refresh token, in bytes.</summary>
    [Range(32, 128)]
    public int TokenSizeBytes { get; set; } = 32;
}
