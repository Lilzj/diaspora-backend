namespace Diaspora.Modules.Guides.Options;

/// <summary>
/// The knobs a deployment may turn.
/// <para>
/// <b>Deliberately few.</b> The domain's ceilings — how many sections, how long a block may be,
/// how many tags a guide carries — are constants in <c>GuideLimits</c> and not options, for the
/// reason that file records: a bound that can be raised per deployment is a bound the domain
/// cannot rely on. What is here is operational rather than behavioural, plus the one genuinely
/// editorial number that a deployment has a legitimate opinion about — how long "trending"
/// means.
/// </para>
/// </summary>
public sealed class GuideOptions
{
    public const string SectionName = "Guides";

    /// <summary>
    /// Whether to seed the guide category catalogue at startup.
    /// <para>
    /// Off in a deployment that curates its own taxonomy and does not want B8's opinion about
    /// what a diaspora knowledge base covers. Seeding is idempotent — it matches on key and
    /// leaves existing categories alone — so leaving it on is safe.
    /// </para>
    /// </summary>
    public bool SeedCategories { get; set; } = true;

    /// <summary>
    /// The trending half-life in days: a guide's engagement score halves every window.
    /// <para>
    /// Editorial rather than technical. A launch community with little content wants a long
    /// window so anything at all trends; a busy one wants a short window so trending means
    /// this week rather than this quarter.
    /// </para>
    /// </summary>
    public int TrendingWindowDays { get; set; } = 14;

    /// <summary>
    /// How many guides one member may hold in draft.
    /// <para>
    /// An abuse ceiling, not an editorial one — published guides are unlimited, because a
    /// prolific verified contributor is exactly what the platform wants. Drafts are what a
    /// script can create for free.
    /// </para>
    /// </summary>
    public int MaxDraftsPerAuthor { get; set; } = 50;

    /// <summary>How often the scheduled publisher looks for guides that have come due.</summary>
    public TimeSpan ScheduledPublisherInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Guides published per scheduled-publisher pass.</summary>
    public int ScheduledPublisherBatchSize { get; set; } = 50;

    /// <summary>
    /// Whether the scheduled publisher runs at all. Off for a single-purpose instance — an
    /// import worker, a test host — that should not be racing the primary one for the same rows.
    /// </summary>
    public bool EnableScheduledPublisher { get; set; } = true;
}
