using Diaspora.Platform.Operations.Caching;

namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// The platform's cache.
/// <para>
/// <b>B17 closed DEBT-013 and rewrote this port in the same commit.</b> Between B1 and PSR-1 this
/// interface was declared and unimplemented — five phases recorded it, three modules shipped their
/// own cache rather than depend on it, and an architecture rule existed solely to stop a fourth.
/// The shape it carried was part of why: a bare string key with an optional <c>TimeSpan</c> cannot
/// express a tag, a tier, or an invalidation, so anything built on it would have needed replacing
/// the day a shared store appeared.
/// </para>
/// <para>
/// The rewrite was free of consequence — no implementation and no caller existed, pinned by
/// <c>No_module_depends_on_a_dormant_kernel_port</c> — and it is the reason a module can now depend
/// on this port without inheriting a migration.
/// </para>
/// <para>
/// <b>A cache failure is not a caller failure.</b> Unless the resolved
/// <see cref="Operations.Policies.CachePolicy"/> says otherwise, every method here swallows a store
/// fault and behaves as a miss. A platform whose shared-cache outage becomes a platform outage has
/// converted an optimisation into a dependency, which is the most common way a caching layer makes
/// availability worse rather than better.
/// </para>
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Reads through the cache, computing the value on a miss.
    /// <para>
    /// The only method most callers need, and the only one that gets stampede protection: on a miss,
    /// one caller per key per process runs <paramref name="factory"/> and the rest wait for its
    /// result. Without that, a popular key expiring sends every in-flight request to the database at
    /// the same instant — the failure mode that turns a cache from a load reducer into a load
    /// amplifier at exactly the wrong moment.
    /// </para>
    /// </summary>
    /// <param name="key">The structured key.</param>
    /// <param name="policyName">
    /// The <see cref="Operations.Policies.CachePolicy"/> to resolve. An unknown name falls back to
    /// the platform default rather than throwing — a cache is not where a request should die.
    /// </param>
    /// <param name="factory">Computes the value. Runs at most once per key per process per miss.</param>
    Task<T> GetOrCreateAsync<T>(
        CacheKey key,
        string policyName,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>The cached value, or <see langword="default"/> on a miss. Null is never itself cached.</summary>
    Task<T?> GetAsync<T>(CacheKey key, string policyName, CancellationToken cancellationToken = default);

    /// <summary>Writes a value under the named policy's expiry, tier and tags.</summary>
    Task SetAsync<T>(
        CacheKey key,
        T value,
        string policyName,
        CancellationToken cancellationToken = default);

    /// <summary>Drops one entry from every tier.</summary>
    Task RemoveAsync(CacheKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops every entry in a namespace.
    /// <para>
    /// Implemented by incrementing the namespace's version rather than by enumerating keys, so its
    /// cost does not grow with the number of entries. See <see cref="CacheKey.ToStorageKey"/>.
    /// </para>
    /// </summary>
    Task RemoveNamespaceAsync(string cacheNamespace, CancellationToken cancellationToken = default);

    /// <summary>Whether an entry exists. Does not extend a sliding expiry.</summary>
    Task<bool> ExistsAsync(CacheKey key, string policyName, CancellationToken cancellationToken = default);
}
