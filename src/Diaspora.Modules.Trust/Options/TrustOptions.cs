using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Trust.Options;

/// <summary>
/// The module's own switches, beside <c>Trust:Decisions</c> (the channel to Identity).
/// <para>
/// Switches only. What counts toward risk, for how long and at which weight is
/// <c>RiskPolicy</c>'s and <c>RiskAssessment</c>'s, in code, so every deployment's High means the
/// same thing.
/// </para>
/// </summary>
public sealed class TrustOptions
{
    public const string SectionName = "Trust";

    /// <summary>
    /// Whether Trust records risk evidence and reassesses members on it (RWD-14, CS-4b).
    /// <para>
    /// Off by default, and turned on per deployment. While on, a member whose risk becomes High has
    /// their points held for review and publishing paused until the evidence ages out. While off,
    /// nothing is recorded and no level changes on new evidence; the sweep below still lets an
    /// assessment made while it was on age out.
    /// </para>
    /// </summary>
    public bool EnableRiskAssessment { get; set; }

    /// <summary>
    /// The risk sweep: reassesses members above the baseline as their evidence ages, and removes
    /// evidence older than the longest window.
    /// <para>
    /// A switch, as every module's loop has. The integration suite turns it off and drives
    /// <c>RunOnceAsync</c> directly, because a background loop querying the one shared SQLite
    /// connection mid-command is a concurrent use SQLite will not allow (DEBT-049).
    /// </para>
    /// </summary>
    public bool EnableRiskSweep { get; set; } = true;

    /// <summary>How often the risk sweep runs.</summary>
    [Range(1, 1440)]
    public int RiskSweepMinutes { get; set; } = 60;
}
