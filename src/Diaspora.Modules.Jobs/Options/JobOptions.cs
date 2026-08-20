namespace Diaspora.Modules.Jobs.Options;

/// <summary>
/// The knobs an operator can turn.
/// <para>
/// <b>Everything here is an abuse bound or a schedule, and nothing here is a domain rule.</b>
/// The distinction is the one <c>JobLimits</c> states from the other side: a bound the aggregate
/// is <em>correct</em> at is a constant in the domain, because an invariant that varies by
/// deployment is not an invariant. What lives here is the set of numbers a deployment genuinely
/// might need to differ on — how many jobs one employer may have open, how often the sweeps run,
/// how long silence counts as ghosting.
/// </para>
/// </summary>
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    /// <summary>
    /// Open postings one employer may hold at once. Generous — a hospital group or an agency
    /// legitimately runs hundreds — and present only so a scripted client cannot publish
    /// forty thousand overnight.
    /// </summary>
    public int MaxOpenPostingsPerEmployer { get; set; } = 500;

    /// <summary>
    /// Applications one member may send in a day.
    /// <para>
    /// A bound on scripted spraying, not on effort. It is set well above what any real job hunt
    /// produces, because the failure mode of setting it low is telling someone genuinely
    /// desperate for work to come back tomorrow.
    /// </para>
    /// </summary>
    public int MaxApplicationsPerCandidatePerDay { get; set; } = 50;

    /// <summary>Drafts one employer may hold. Cheap to create, so worth bounding.</summary>
    public int MaxDraftsPerEmployer { get; set; } = 200;

    /// <summary>
    /// How long an unanswered application waits before it counts against an employer's public
    /// ghosting rate. Fourteen days is long enough to be unarguable.
    /// </summary>
    public int GhostingThresholdDays { get; set; } = 14;

    /// <summary>How often the expiry sweep runs, in minutes.</summary>
    public int ExpirySweepIntervalMinutes { get; set; } = 15;

    /// <summary>How many postings one expiry pass may take.</summary>
    public int ExpirySweepBatchSize { get; set; } = 200;

    /// <summary>How often the alert sweep runs, in minutes.</summary>
    public int AlertSweepIntervalMinutes { get; set; } = 15;

    /// <summary>How many alerts one pass may take. Bounded so a pass is a bounded amount of work.</summary>
    public int AlertSweepBatchSize { get; set; } = 200;

    /// <summary>Matches one alert digest may carry. Past this the digest says "and N more".</summary>
    public int MaxMatchesPerAlertDigest { get; set; } = 20;

    /// <summary>How often the offer-expiry sweep runs, in minutes.</summary>
    public int OfferExpirySweepIntervalMinutes { get; set; } = 30;

    /// <summary>How many hours before a saved job's closing date the reminder is sent.</summary>
    public int SavedJobReminderLeadHours { get; set; } = 48;

    /// <summary>
    /// Whether the module seeds its category catalogue at startup. Off for a deployment whose
    /// migrations and seed data are applied by a separate job.
    /// </summary>
    public bool SeedCategories { get; set; } = true;

    /// <summary>
    /// Whether the background sweeps run in this process. Off for a web instance in a
    /// deployment that runs them in a dedicated worker.
    /// </summary>
    public bool EnableBackgroundSweeps { get; set; } = true;
}
