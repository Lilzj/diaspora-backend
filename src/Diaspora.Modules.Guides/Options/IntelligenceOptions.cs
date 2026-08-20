namespace Diaspora.Modules.Guides.Options;

/// <summary>
/// The knobs a deployment may turn on the Country Intelligence Platform.
/// <para>
/// <b>Operational, not behavioural</b> — the rule <c>KnowledgeOptions</c> states and this file
/// follows. Every ceiling the aggregates enforce is a constant in <c>IntelligenceLimits</c>, because
/// a bound that can be raised per deployment is a bound the domain cannot rely on. What is here is
/// seeding, sweeping and the two genuinely editorial windows a deployment has a legitimate opinion
/// about.
/// </para>
/// <para>
/// <b>There is deliberately no option that relaxes the publication gate.</b> A notice needs a
/// verified authority, and a deployment that could switch that off would be publishing unverified
/// claims under an official badge — which is the one thing this phase exists to make impossible, and
/// not a thing a configuration file should be able to undo.
/// </para>
/// </summary>
public sealed class IntelligenceOptions
{
    public const string SectionName = "Intelligence";

    /// <summary>
    /// Whether to seed the notice category catalogue at startup.
    /// <para>
    /// Off in a deployment that curates its own vocabulary. Seeding is idempotent — it matches on
    /// key and leaves existing rows alone — so leaving it on is safe.
    /// </para>
    /// </summary>
    public bool SeedCategories { get; set; } = true;

    /// <summary>
    /// Whether the retirement sweep runs.
    /// <para>
    /// Off in the integration suite, and for the reason every background loop on this platform is:
    /// the suite shares one in-memory SQLite connection, which a loop querying it mid-command would
    /// be a concurrent use of, and a test asserting on a side effect after a timer happens to have
    /// fired is a flaky test wearing a passing badge. Tests drive <c>RunOnceAsync</c> directly.
    /// </para>
    /// </summary>
    public bool EnableExpirySweep { get; set; } = true;

    /// <summary>How often the retirement sweep looks for notices whose expiry has passed.</summary>
    public TimeSpan ExpirySweepInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How many notices one sweep pass retires.
    /// <para>
    /// Bounded so a neglected deployment's first pass cannot load its whole archive into memory, and
    /// so the sweep's cost is predictable however far behind it is. The next pass takes the next
    /// batch; nothing is lost by being slow here.
    /// </para>
    /// </summary>
    public int ExpirySweepBatchSize { get; set; } = 200;

    /// <summary>
    /// Whether composed country intelligence reads are cached.
    /// <para>
    /// Off is the right choice for the integration suite, where a cached dashboard would make a test
    /// that publishes a notice and then reads the country page assert against the page before it.
    /// </para>
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// How long a composed intelligence dashboard may be served from cache.
    /// <para>
    /// <b>Shorter than the knowledge directory's, and the difference is the point.</b> A country's
    /// guides change weekly; a country's emergency alerts change during an emergency. Two minutes of
    /// staleness on a border closure is about the most a reader should ever wear, and invalidation on
    /// publication is exact anyway — this window only covers what invalidation cannot see.
    /// </para>
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How far ahead a country dashboard looks for public holidays, in days.
    /// <para>
    /// Editorial rather than technical. The question a dashboard answers is "should I plan around
    /// anything", and the horizon somebody plans an appointment over is a quarter — but a deployment
    /// serving a community that books consular appointments a year ahead is entitled to disagree.
    /// </para>
    /// </summary>
    public int HolidayHorizonDays { get; set; } = 90;

    /// <summary>
    /// Whether usage signals are recorded at all.
    /// <para>
    /// On by default, because an editorial team with no idea whether anybody saw an advisory cannot
    /// do its job. Off for a deployment whose policy is to retain nothing about what was read, even
    /// in aggregate and even without any reader identity — a position a deployment is entitled to
    /// hold and should not have to patch code to hold.
    /// </para>
    /// </summary>
    public bool RecordAnalytics { get; set; } = true;
}
