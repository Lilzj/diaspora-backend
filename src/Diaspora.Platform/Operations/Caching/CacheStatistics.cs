namespace Diaspora.Platform.Operations.Caching;

/// <summary>
/// What one cache namespace has done since the process started.
/// </summary>
/// <param name="Namespace">The family these counts belong to.</param>
/// <param name="Hits">Reads served from any tier, excluding stale serves.</param>
/// <param name="Misses">Reads that reached the factory.</param>
/// <param name="StaleServes">Expired entries served while a refresh ran.</param>
/// <param name="Writes">Entries written.</param>
/// <param name="Invalidations">Entries dropped by tag, version or key.</param>
/// <param name="Errors">Store faults swallowed. Non-zero here with a healthy hit ratio means the
/// shared tier is failing and the local tier is hiding it.</param>
public readonly record struct CacheNamespaceStatistics(
    string Namespace,
    long Hits,
    long Misses,
    long StaleServes,
    long Writes,
    long Invalidations,
    long Errors)
{
    /// <summary>Hits over hits plus misses. Zero when nothing has been read.</summary>
    public double HitRatio => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
}

/// <summary>
/// The whole cache's state, as the operational dashboard sees it.
/// </summary>
/// <param name="LocalProvider">Name of the in-process store.</param>
/// <param name="DistributedProvider">
/// Name of the shared store, or <see langword="null"/> when none is registered. Null is the
/// interesting case: it means every <c>Distributed</c> and <c>MultiLevel</c> policy is being served
/// from process memory, so entries differ between replicas and an invalidation reaches one of them.
/// The dashboard says so rather than reporting a healthy hit ratio over an inconsistent cache.
/// </param>
/// <param name="Namespaces">Per-family counts.</param>
public sealed record CacheStatistics(
    string LocalProvider,
    string? DistributedProvider,
    IReadOnlyList<CacheNamespaceStatistics> Namespaces)
{
    /// <summary>Whether the shared tier is present. False means cross-replica consistency is not available.</summary>
    public bool IsDistributed => DistributedProvider is not null;

    public long Hits => Namespaces.Sum(entry => entry.Hits);

    public long Misses => Namespaces.Sum(entry => entry.Misses);

    public long Errors => Namespaces.Sum(entry => entry.Errors);

    public double HitRatio => Hits + Misses == 0 ? 0 : (double)Hits / (Hits + Misses);
}

/// <summary>Reads the cache's counters without being able to change them.</summary>
public interface ICacheStatisticsProvider
{
    CacheStatistics Snapshot();
}
