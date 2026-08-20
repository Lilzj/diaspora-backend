namespace Diaspora.Modules.Recommendations.Options;

/// <summary>
/// Everything about this module a deployment may change without a code change.
/// <para>
/// <b>The defaults are the shipping behaviour, and every one of them is a judgement rather than a
/// round number.</b> A deployment that sets nothing gets a platform that recommends sensibly; the
/// knobs exist because "how long is a dismissal honoured" is a product decision that will be argued
/// about, and arguing about it should not require a release.
/// </para>
/// </summary>
public sealed class RecommendationOptions
{
    public const string SectionName = "Recommendations";

    /// <summary>How many items a surface returns when the caller asks for no particular number.</summary>
    public int DefaultPageSize { get; set; } = 20;

    /// <summary>The ceiling a caller cannot ask past. Protects the ranking pass, not the database.</summary>
    public int MaximumPageSize { get; set; } = 100;

    /// <summary>
    /// How many candidates are pulled into a ranking pass before policies and diversity cut it down.
    /// <para>
    /// Larger than the page by a wide margin on purpose: policies exclude, diversity demotes, and a
    /// pool the size of the page would return three items after both ran.
    /// </para>
    /// </summary>
    public int CandidatePoolSize { get; set; } = 400;

    /// <summary>How long a generated set stays valid before the next request regenerates it.</summary>
    public TimeSpan SetLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a dismissal suppresses a target. Null means forever.</summary>
    public TimeSpan? DismissalLifetime { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// How long a signal is kept before the retention sweep deletes it.
    /// <para>
    /// The privacy floor of the whole module. A signal is a record of what a member looked at, and
    /// keeping it forever because storage is cheap is how a personalization platform becomes a
    /// surveillance one.
    /// </para>
    /// </summary>
    public TimeSpan SignalRetention { get; set; } = TimeSpan.FromDays(365);

    /// <summary>How long a generated set is kept before the retention sweep deletes it.</summary>
    public TimeSpan SetRetention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>The soonest a member may ask for a full profile rebuild again.</summary>
    public TimeSpan RebuildCooldown { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>How many signals one client request may record at once.</summary>
    public int MaximumSignalBatchSize { get; set; } = 50;

    /// <summary>How many signals one incremental profile build reads.</summary>
    public int ProfileBuildSignalWindow { get; set; } = 2000;

    /// <summary>
    /// How many items from one author or one category a single page may contain.
    /// <para>
    /// The diversity floor. Without it the most popular employer in a country takes the whole jobs
    /// surface, which is both a worse product and a fairness problem.
    /// </para>
    /// </summary>
    public int MaximumItemsPerGroup { get; set; } = 3;

    /// <summary>How stale a profile may be before the refresh sweep rebuilds it.</summary>
    public TimeSpan ProfileStaleAfter { get; set; } = TimeSpan.FromHours(6);

    /// <summary>How long a cached feed response is served before it is recomputed.</summary>
    public TimeSpan CacheLifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How many profiles one refresh sweep pass rebuilds.</summary>
    public int ProfileSweepBatchSize { get; set; } = 100;

    /// <summary>How often the profile refresh sweep runs.</summary>
    public TimeSpan ProfileSweepInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How often the retention sweep runs.</summary>
    public TimeSpan RetentionSweepInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>How often the analytics rollup sweep runs.</summary>
    public TimeSpan AnalyticsSweepInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How often the candidate pruning sweep runs.</summary>
    public TimeSpan CandidateSweepInterval { get; set; } = TimeSpan.FromHours(12);

    /// <summary>Turns the profile refresh sweep off. The integration suite drives it directly.</summary>
    public bool EnableProfileSweep { get; set; } = true;

    /// <summary>Turns the retention sweep off. The integration suite drives it directly.</summary>
    public bool EnableRetentionSweep { get; set; } = true;

    /// <summary>Turns the analytics rollup sweep off. The integration suite drives it directly.</summary>
    public bool EnableAnalyticsSweep { get; set; } = true;

    /// <summary>Turns the candidate pruning sweep off. The integration suite drives it directly.</summary>
    public bool EnableCandidateSweep { get; set; } = true;

    /// <summary>
    /// Whether a generated set is persisted.
    /// <para>
    /// On by default because explainability is a contract: a member who taps "why am I seeing this"
    /// three screens later must get the same answer, and an answer recomputed from a profile that
    /// has moved on since is a different answer.
    /// </para>
    /// </summary>
    public bool PersistGeneratedSets { get; set; } = true;
}
