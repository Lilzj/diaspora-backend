namespace Diaspora.Platform.Operations.Caching;

/// <summary>Why a cache entry was dropped. Recorded so an unexplained miss rate can be explained.</summary>
public enum CacheInvalidationReason
{
    /// <summary>A tag was invalidated.</summary>
    Tag = 0,

    /// <summary>A namespace's version was incremented.</summary>
    Version = 1,

    /// <summary>An explicit key was removed.</summary>
    Key = 2,

    /// <summary>An integration event carried a fact that made entries untrue.</summary>
    Event = 3,

    /// <summary>An operator asked for it.</summary>
    Manual = 4
}

/// <summary>
/// The four ways a cached value stops being believed, behind one contract.
/// <para>
/// Part 3 names them: tag-based, event-based, time-based, manual, version-based. Time-based needs no
/// method — it is the policy's expiry and it happens without anyone asking. The other four are here.
/// </para>
/// <para>
/// <b>Invalidation is broadcast, not local.</b> On a multi-level cache the local tier of every other
/// replica holds a copy this process cannot reach, so an invalidation that only clears the local
/// process is an invalidation that fixes one of N replicas. The implementation writes the
/// invalidation to the shared tier and every replica observes it — which is why an invalidation is
/// asynchronous and why the guide says a caller must not read back immediately and expect a miss.
/// </para>
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>Drops every entry carrying <paramref name="tag"/>, on every replica.</summary>
    Task InvalidateTagAsync(
        string tag,
        CacheInvalidationReason reason = CacheInvalidationReason.Tag,
        CancellationToken cancellationToken = default);

    /// <summary>Drops every entry in a namespace by incrementing its version.</summary>
    Task InvalidateNamespaceAsync(
        string cacheNamespace,
        CacheInvalidationReason reason = CacheInvalidationReason.Version,
        CancellationToken cancellationToken = default);

    /// <summary>Drops one entry.</summary>
    Task InvalidateKeyAsync(
        CacheKey key,
        CacheInvalidationReason reason = CacheInvalidationReason.Key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops everything. The operator's last resort, and a genuinely dangerous button: a platform
    /// that has just lost its whole cache sends every read to the database at once.
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken = default);
}
