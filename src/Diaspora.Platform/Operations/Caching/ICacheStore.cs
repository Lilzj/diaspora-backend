namespace Diaspora.Platform.Operations.Caching;

/// <summary>Where a value was found. Reported on the read path and aggregated into the hit ratio.</summary>
public enum CacheSource
{
    /// <summary>Not found anywhere.</summary>
    Miss = 0,

    /// <summary>Found in the in-process tier.</summary>
    Local = 1,

    /// <summary>Found in the shared tier.</summary>
    Distributed = 2,

    /// <summary>
    /// Found expired and served anyway under <c>StaleWhileRevalidate</c>, with a refresh in flight.
    /// Counted separately: a deployment whose stale rate is climbing has a source that is slower
    /// than its policy assumes, and a plain hit ratio hides that completely.
    /// </summary>
    Stale = 3
}

/// <summary>What a store read returned.</summary>
/// <param name="Source">Where it came from. <see cref="CacheSource.Miss"/> means <paramref name="Value"/> is meaningless.</param>
/// <param name="Value">The stored bytes, or empty on a miss.</param>
/// <param name="Age">How long the entry has been in the store. Used to decide staleness.</param>
public readonly record struct CacheStoreRead(
    CacheSource Source,
    ReadOnlyMemory<byte> Value,
    TimeSpan Age)
{
    public static CacheStoreRead Miss => new(CacheSource.Miss, ReadOnlyMemory<byte>.Empty, TimeSpan.Zero);

    public bool Found => Source != CacheSource.Miss;
}

/// <summary>
/// The provider seam. One class per backing store, and nothing above it knows which one is loaded.
/// <para>
/// <b>Bytes, not objects.</b> Serialization belongs above the store so that every store shares one
/// format and a value written by the in-memory store during a test is byte-identical to one written
/// by Redis in production. A store that serialized would let the two drift, and the drift would only
/// appear when a deployment switched.
/// </para>
/// <para>
/// The contract is deliberately small. Everything interesting — tags, policies, multi-level reads,
/// stale-while-revalidate, metrics — is composed above this by the platform, so a Redis adapter is
/// five methods rather than a reimplementation of the cache.
/// </para>
/// </summary>
public interface ICacheStore
{
    /// <summary>Which tier this store serves. The multi-level cache orders its stores by this.</summary>
    CacheStoreTier Tier { get; }

    /// <summary>A name for diagnostics and metric labels — <c>memory</c>, <c>redis</c>.</summary>
    string Provider { get; }

    Task<CacheStoreRead> GetAsync(string storageKey, CancellationToken cancellationToken = default);

    Task SetAsync(
        string storageKey,
        ReadOnlyMemory<byte> value,
        TimeSpan absoluteExpiration,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the store is currently answering. Read by the health check and by the breaker, not
    /// by the read path — a cache that pings before every read has doubled its own latency.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Which tier a store serves.
/// <para>
/// Distinct from <see cref="Policies.CacheTier"/>, which has a third member — <c>MultiLevel</c> — that
/// no single store can be. A policy asks for both tiers; a store is one of them. Two enums rather
/// than one because collapsing them would make <c>MultiLevel</c> a legal answer to "what kind of
/// store are you", and the composite is not a store.
/// </para>
/// </summary>
public enum CacheStoreTier
{
    Local = 0,
    Distributed = 1
}
