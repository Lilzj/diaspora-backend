using System.ComponentModel.DataAnnotations;

namespace Diaspora.Operations.Caching;

/// <summary>
/// How the cache is wired on this deployment.
/// <para>
/// Everything here is a deployment concern rather than a policy concern. A <c>CachePolicy</c> says
/// how long a guide is cached; this says which prefix the keys carry, how much memory the local tier
/// may take, and how quickly an invalidation propagates.
/// </para>
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Prefixes every storage key.
    /// <para>
    /// Two environments pointed at one shared store without distinct prefixes read each other's
    /// entries. That is usually discovered when a staging feature-flag snapshot appears in
    /// production, by which point it has been true for a while.
    /// </para>
    /// </summary>
    [Required]
    [RegularExpression("^[a-z0-9][a-z0-9._-]{0,31}$")]
    public string KeyPrefix { get; set; } = "diaspora";

    /// <summary>Whether caching is on at all. Off makes every read a miss and every write a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Entries the local tier holds before it starts evicting. Counted in entries rather than bytes
    /// because the runtime cannot cheaply measure the size of an arbitrary object graph, and a size
    /// limit computed from a guess is a limit nobody can reason about.
    /// </summary>
    [Range(64, 10_000_000)]
    public int LocalEntryLimit { get; set; } = 20_000;

    /// <summary>
    /// How long a namespace or tag version is trusted before it is re-read from the shared store.
    /// <para>
    /// <b>This is the propagation delay of every tag and namespace invalidation</b>, and it is a
    /// direct trade against read cost: zero means every cached read also reads a version key. Five
    /// seconds is the default because the families that cannot tolerate five seconds of staleness —
    /// feature flags, safety controls — carry policies with no tags and a local-only tier, so they
    /// never take this path.
    /// </para>
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan VersionRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether cache warming runs at startup. Off in test hosts, where warming a family means
    /// running a query against a database the test has not seeded yet.
    /// </summary>
    public bool WarmOnStartup { get; set; } = true;

    /// <summary>
    /// Ceiling on the whole warming pass. A warmer that hangs must not hold up readiness — the
    /// instance is perfectly able to serve cold.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:10:00")]
    public TimeSpan WarmupTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
