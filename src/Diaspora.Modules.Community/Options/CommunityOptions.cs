namespace Diaspora.Modules.Community.Options;

/// <summary>
/// The module's operational settings.
/// <para>
/// Only things a deployment genuinely varies. Domain limits — body lengths, attachment counts,
/// confirmation thresholds — are deliberately <b>not</b> here: they are product decisions pinned
/// against the client by test, and making them configurable would let a deployment silently
/// disagree with what a member sees on their screen.
/// </para>
/// </summary>
public sealed class CommunityOptions
{
    public const string SectionName = "Community";

    /// <summary>
    /// Whether this deployment runs <c>CommunitySweeper</c> at all.
    /// <para>
    /// On by default: expiry is the mechanism that makes a hyperlocal feed trustworthy, and a
    /// deployment that turned it off would accumulate stale traffic alerts for ever.
    /// </para>
    /// <para>
    /// <b>Added in PSR-1 Final Certification.</b> Every other background loop on the platform has
    /// had one of these since its own phase, and this sweep was one of six that did not — which is
    /// why the integration suite could not switch it off. That matters more than it sounds: the
    /// suite shares one in-memory SQLite connection, and a background loop querying it while a
    /// request is mid-command is a concurrent use of a connection SQLite will not allow. DEBT-049
    /// recorded the resulting non-determinism across three separate observations.
    /// </para>
    /// </summary>
    public bool EnableExpirySweep { get; set; } = true;

    /// <summary>How often the expiry sweep runs.</summary>
    public int ExpirySweepIntervalMinutes { get; set; } = 15;

    /// <summary>Posts expired per sweep pass. Bounded so one pass cannot hold a connection.</summary>
    public int ExpirySweepBatchSize { get; set; } = 500;

    /// <summary>How often the trending window rolls over.</summary>
    public int TrendingWindowHours { get; set; } = 24;

    /// <summary>Hashtags rolled per pass.</summary>
    public int TrendingRollBatchSize { get; set; } = 1_000;

    /// <summary>
    /// Whether the module seeds its built-in reaction definitions on startup.
    /// <para>
    /// On by default, because a platform with no reactions has no reaction picker at all. A
    /// deployment that has curated its own catalogue turns this off rather than deleting rows a
    /// restart would recreate.
    /// </para>
    /// </summary>
    public bool SeedReactionCatalog { get; set; } = true;
}
