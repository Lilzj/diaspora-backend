using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// Failed sign-in throttling.
/// </summary>
public sealed class LockoutOptions
{
    public const string SectionName = "Identity:Lockout";

    [Range(1, 20)]
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// How long the lockout lasts. Self-healing on a timer rather than requiring support
    /// intervention: an attacker guessing passwords should cost the member a few minutes,
    /// not their access to the platform.
    /// </summary>
    [Range(1, 1440)]
    public int LockoutMinutes { get; set; } = 15;
}
