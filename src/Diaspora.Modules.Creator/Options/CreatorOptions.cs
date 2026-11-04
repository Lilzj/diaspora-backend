using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Creator.Options;

/// <summary>
/// The module's configuration.
/// <para>
/// Deliberately small. Nothing that decides a status, a reward value or an eligibility rule is here
/// — those are versioned policies in code, because a deployment that quietly moved a threshold would
/// make one member's Established Creator a different achievement from another's.
/// </para>
/// <para>
/// What is here is a deployment's own facts: where a share link points, and whether the background
/// sweeps run.
/// </para>
/// </summary>
public sealed class CreatorOptions
{
    public const string SectionName = "Creator";

    /// <summary>
    /// The base a referral share link is built from.
    /// <para>
    /// Server-built rather than client-composed, which is the register's own requirement: "The full
    /// share URL, built by the server so the domain is never guessed."
    /// </para>
    /// </summary>
    [Required]
    public string ReferralShareBaseUrl { get; set; } = "https://diaspora.app/join";

    /// <summary>
    /// The points a new member's sign-up bonus offers.
    /// <para>
    /// Configuration rather than a constant because it is a launch decision a deployment makes, and
    /// unlike a status threshold it does not change what an achievement means — it changes what the
    /// platform is willing to spend to acquire a member.
    /// </para>
    /// </summary>
    [Range(0, 100_000)]
    public int SignupBonusPoints { get; set; } = 100;

    /// <summary>How long a new member has to acknowledge the sign-up bonus before it lapses.</summary>
    [Range(1, 365)]
    public int SignupBonusValidDays { get; set; } = 30;

    /// <summary>
    /// The lifecycle sweep: expires lapsed verifications, referrals and bonuses.
    /// <para>
    /// A switch, on the module's first day rather than four phases in — DEBT-049's lesson. The
    /// integration suite turns it off and drives <c>RunOnceAsync</c> directly, because a background
    /// loop querying the one shared SQLite connection mid-command is a concurrent use SQLite will
    /// not allow.
    /// </para>
    /// </summary>
    public bool EnableLifecycleSweep { get; set; } = true;

    /// <summary>How often the lifecycle sweep runs.</summary>
    [Range(1, 1440)]
    public int LifecycleSweepMinutes { get; set; } = 60;

    /// <summary>
    /// Whether the catalogue seeder writes its two sample grant programmes. Off by default since
    /// 4 Nov 2026: members took the two the seeder wrote at every startup, in every environment, for
    /// the platform's own, in pounds wherever they lived. A deployment's grants are the ones its
    /// administrators post through <c>/grants/admin</c>. A test or demo host turns this on.
    /// </summary>
    public bool SeedSampleGrants { get; set; }
}
