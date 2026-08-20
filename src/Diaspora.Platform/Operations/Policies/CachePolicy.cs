namespace Diaspora.Platform.Operations.Policies;

/// <summary>Which tiers of the cache an entry is allowed to occupy.</summary>
public enum CacheTier
{
    /// <summary>In-process only. Fastest, and inconsistent across replicas by construction.</summary>
    Local = 0,

    /// <summary>Shared store only. Consistent across replicas, one network hop per read.</summary>
    Distributed = 1,

    /// <summary>
    /// Local in front of distributed. The default for read-heavy reference data, and the tier that
    /// needs an invalidation story: a local copy on replica three does not hear about a write on
    /// replica one unless something tells it.
    /// </summary>
    MultiLevel = 2
}

/// <summary>
/// How long a thing is cached, where, and what invalidates it.
/// <para>
/// Every cached read on the platform names one of these. That is what makes DEBT-013's closure
/// meaningful: implementing <c>ICacheService</c> without a policy vocabulary would have given
/// every module its own opinion about expiry, which is how a platform ends up with a country list
/// cached for five seconds and a payment-provider token cached for a day.
/// </para>
/// <para>
/// <b>Stale-while-revalidate is opt-in and it is the interesting knob.</b> With it, an expired
/// entry is served once more while a single caller refreshes it — which converts a thundering herd
/// on expiry into one refresh, at the cost of one stale answer. Reference data wants it. A wallet
/// balance must never have it, and the validator will not stop you: no policy can know that a
/// value is money. The rule is in the guide and the review.
/// </para>
/// </summary>
public sealed record CachePolicy : OperationalPolicy
{
    public const string PolicyKind = "cache";

    public CachePolicy(string name, int version = 1) : base(name, version)
    {
    }

    public override string Kind => PolicyKind;

    /// <summary>Where entries under this policy live.</summary>
    public CacheTier Tier { get; init; } = CacheTier.MultiLevel;

    /// <summary>Absolute lifetime. The only expiry that bounds staleness; sliding expiry does not.</summary>
    public TimeSpan AbsoluteExpiration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Idle lifetime, renewed on each read. <see langword="null"/> disables it. A sliding-only
    /// entry on a hot key never expires, which is why <see cref="AbsoluteExpiration"/> is not optional.
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>
    /// Lifetime of the local copy in a <see cref="CacheTier.MultiLevel"/> policy. Shorter than
    /// <see cref="AbsoluteExpiration"/> by default: the local tier is the one that goes stale
    /// invisibly, so it should be the one that expires sooner.
    /// </summary>
    public TimeSpan? LocalExpiration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tags every entry under this policy carries. Invalidating a tag drops every entry that has
    /// it, which is how "a guide changed" clears the country feed, the trending list and the
    /// guide's own entry without any of them knowing about the others.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// How long an expired entry may still be served while one caller refreshes it.
    /// <see langword="null"/> disables the behaviour.
    /// </summary>
    public TimeSpan? StaleWhileRevalidate { get; init; }

    /// <summary>Whether this policy's keys participate in cache warming at startup.</summary>
    public bool WarmOnStartup { get; init; }

    /// <summary>
    /// Whether a cache failure is allowed to fail the caller. Almost always <see langword="false"/>:
    /// a cache is an optimisation, and a platform whose Redis outage becomes a platform outage has
    /// converted an optimisation into a dependency.
    /// </summary>
    public bool FailOnCacheError { get; init; }

    public override IEnumerable<string> Validate()
    {
        if (AbsoluteExpiration <= TimeSpan.Zero)
        {
            yield return "AbsoluteExpiration must be positive; an entry with no absolute bound can be stale forever.";
        }

        if (SlidingExpiration is { } sliding)
        {
            if (sliding <= TimeSpan.Zero)
            {
                yield return "SlidingExpiration must be positive when set.";
            }
            else if (sliding > AbsoluteExpiration)
            {
                yield return "SlidingExpiration exceeds AbsoluteExpiration, so it can never take effect.";
            }
        }

        if (LocalExpiration is { } local)
        {
            if (local <= TimeSpan.Zero)
            {
                yield return "LocalExpiration must be positive when set.";
            }
            else if (local > AbsoluteExpiration)
            {
                yield return "LocalExpiration exceeds AbsoluteExpiration, so the local tier outlives the shared one.";
            }
        }

        if (Tier != CacheTier.MultiLevel && LocalExpiration is not null && Tier != CacheTier.Local)
        {
            yield return "LocalExpiration is only meaningful for a local or multi-level policy.";
        }

        if (StaleWhileRevalidate is { } stale && stale <= TimeSpan.Zero)
        {
            yield return "StaleWhileRevalidate must be positive when set.";
        }

        foreach (var tag in Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                yield return "A cache tag cannot be blank.";
            }
        }
    }
}
