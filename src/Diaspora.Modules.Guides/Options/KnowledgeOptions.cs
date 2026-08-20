namespace Diaspora.Modules.Guides.Options;

/// <summary>
/// The knobs a deployment may turn on the Country Knowledge Directory.
/// <para>
/// <b>Operational, not behavioural.</b> Every ceiling the aggregates enforce is a constant in
/// <c>KnowledgeLimits</c>, for the reason that file records: a bound that can be raised per
/// deployment is a bound the domain cannot rely on. What is here is caching, seeding and the two
/// genuinely editorial windows a deployment has a legitimate opinion about — how recent "trending"
/// means, and how long a composed country page may be served from cache.
/// </para>
/// </summary>
public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    /// <summary>
    /// Whether to seed the navigation catalogue at startup.
    /// <para>
    /// Off in a deployment that curates its own navigation and does not want this platform's
    /// opinion about what a diaspora needs to know. Seeding is idempotent — it matches on key and
    /// leaves existing rows alone — so leaving it on is safe.
    /// </para>
    /// </summary>
    public bool SeedCategories { get; set; } = true;

    /// <summary>
    /// Whether a new country directory is arranged from a template.
    /// <para>
    /// Off for a deployment whose curators want to arrange every country by hand from an empty
    /// page. On, a new country opens with a sensible ordering that the curator then edits, which is
    /// most of an afternoon saved per country launch.
    /// </para>
    /// </summary>
    public bool ApplyDirectoryTemplates { get; set; } = true;

    /// <summary>
    /// Whether composed directory reads are cached at all.
    /// <para>
    /// Off is a legitimate production choice for a small deployment and the right choice for the
    /// integration suite, where a cached country page would make a test that arranges a directory
    /// and then reads it back assert against the previous arrangement.
    /// </para>
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// How long a composed country page may be served from cache.
    /// <para>
    /// Short by the standards of a page that changes weekly, and deliberately: invalidation on
    /// curation is exact, so this window only ever covers the changes invalidation cannot see —
    /// a guide being published, a job being posted, a counter moving. Five minutes of staleness on
    /// a headline number is invisible; an hour of it is a support ticket.
    /// </para>
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Entries the shipped in-memory cache holds before it evicts the oldest.</summary>
    public int CacheCapacity { get; set; } = 1_000;

    /// <summary>
    /// How far back trending topics look.
    /// <para>
    /// Editorial rather than technical, exactly like B8's trending window. A launch community wants
    /// a long window so anything at all trends; a busy one wants a short window so trending means
    /// this week rather than this quarter.
    /// </para>
    /// </summary>
    public int TrendingWindowDays { get; set; } = 30;

    /// <summary>
    /// Whether search phrases are counted for analytics.
    /// <para>
    /// On by default because unanswered searches are the best commissioning signal the platform
    /// has. Off for a deployment whose policy is to retain nothing about what people looked for,
    /// even in aggregate — the phrases are already stored without any reader identity, but a
    /// deployment is entitled to that position and should not have to patch code to hold it.
    /// </para>
    /// </summary>
    public bool RecordSearchTerms { get; set; } = true;
}
