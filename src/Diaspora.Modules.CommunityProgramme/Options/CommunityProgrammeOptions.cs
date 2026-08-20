using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.CommunityProgramme.Options;

/// <summary>
/// What a deployment can change about the Community Programme module without a release.
/// <para>
/// Every sweep has a switch, and that is not a preference — DEBT-049 recorded three separate
/// occasions where a background loop querying the test host's one shared SQLite connection while a
/// request was mid-command produced a failure nobody could reproduce. Six loops on this platform ran
/// unconditionally because no switch existed; PSR-1 added theirs, and this module ships with its own
/// from the first day.
/// </para>
/// </summary>
public sealed class CommunityProgrammeOptions
{
    public const string SectionName = "CommunityProgramme";

    /// <summary>
    /// Whether the lifecycle sweep runs.
    /// <para>
    /// It moves memberships to <c>RenewalDue</c> and <c>Expired</c>, opens scheduled polls, closes
    /// expired ones and closes opportunities past their deadline. Turning it off does not corrupt
    /// anything — every one of those is a state the sweep names rather than a fact it invents — but a
    /// deployment with it off will show a poll as open past its closing time.
    /// </para>
    /// </summary>
    public bool EnableLifecycleSweep { get; set; } = true;

    /// <summary>How often the lifecycle sweep runs, in minutes.</summary>
    [Range(1, 1440)]
    public int LifecycleSweepMinutes { get; set; } = 15;

    /// <summary>
    /// Whether the counter reconciliation sweep runs.
    /// <para>
    /// Recounts filled places on tiers, opportunities and shifts, and the response total on a poll.
    /// A recount rather than a delta, so it repairs drift instead of compounding it — and so a
    /// deployment that had it off for a week is correct the first time it comes back on.
    /// </para>
    /// </summary>
    public bool EnableReconciliationSweep { get; set; } = true;

    /// <summary>How often the reconciliation sweep runs, in minutes.</summary>
    [Range(5, 1440)]
    public int ReconciliationSweepMinutes { get; set; } = 60;

    /// <summary>How many rows either sweep takes in one pass.</summary>
    [Range(10, 1000)]
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// The key a poll voter's fingerprint is derived with.
    /// <para>
    /// <b>Change it and every anonymous poll's uniqueness rule resets</b>, because the fingerprints
    /// already stored were derived with the old key and a member's new one will not collide with
    /// their old one. That is a deliberate consequence rather than an oversight: rotating this is a
    /// decision to let everybody vote again, and there is no way to rotate it that is not.
    /// </para>
    /// <para>
    /// The default is a fixed development value. A deployment that leaves it is not made insecure —
    /// the hash protects a voter's identity from the database, not from an operator who can read the
    /// configuration — but it does mean two deployments sharing a database would agree on
    /// fingerprints, which is the one case where it matters.
    /// </para>
    /// </summary>
    [Required]
    [MinLength(16)]
    public string PollFingerprintSecret { get; set; } =
        "diaspora-community-programme-poll-fingerprint";

    /// <summary>
    /// How many days of announcements the dashboard's "recent" count covers.
    /// </summary>
    [Range(1, 365)]
    public int RecentAnnouncementDays { get; set; } = 30;
}
