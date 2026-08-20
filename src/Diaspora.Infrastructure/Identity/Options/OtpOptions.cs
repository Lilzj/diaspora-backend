using System.ComponentModel.DataAnnotations;

namespace Diaspora.Infrastructure.Identity.Options;

/// <summary>
/// One-time code generation, lifetime and abuse limits.
/// </summary>
public sealed class OtpOptions
{
    public const string SectionName = "Identity:Otp";

    /// <summary>
    /// Digits per code. Six is the usability floor for SMS; brute force is prevented by
    /// the attempt budget below, not by code length.
    /// </summary>
    [Range(4, 10)]
    public int CodeLength { get; set; } = 6;

    [Range(1, 60)]
    public int LifetimeMinutes { get; set; } = 10;

    /// <summary>Wrong answers permitted before the challenge is retired.</summary>
    [Range(1, 10)]
    public int MaxVerificationAttempts { get; set; } = 5;

    [Range(0, 10)]
    public int MaxResends { get; set; } = 3;

    /// <summary>
    /// Seconds a member must wait before a resend is accepted. Zero disables the cooldown;
    /// the resend budget above still caps abuse, so a zero here is a deliberate
    /// configuration (test hosts use it) rather than an unbounded hole.
    /// </summary>
    [Range(0, 600)]
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>
    /// Codes a member may trigger inside <see cref="ThrottleWindowMinutes"/>, across
    /// issue and resend alike. Caps the cost of someone aiming the endpoint at a phone
    /// number that is not theirs.
    /// </summary>
    [Range(1, 50)]
    public int MaxRequestsPerWindow { get; set; } = 5;

    [Range(1, 1440)]
    public int ThrottleWindowMinutes { get; set; } = 60;
}
