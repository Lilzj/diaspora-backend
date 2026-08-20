namespace Diaspora.Modules.Recommendations.Extensibility.Caching;

/// <summary>
/// A place to keep something that was expensive to work out.
/// <para>
/// <b>Declared here rather than taken from the kernel, and the reason is a defect the platform has
/// already paid for once.</b> The kernel declares <c>ICacheService</c> and nothing implements it;
/// B13 recorded that depending on it makes a component fail silently. This module ships its own
/// abstraction and its own in-memory implementation, so the seam a deployment replaces with Redis is
/// one that works before it is replaced.
/// </para>
/// <para>
/// <b>What is cached is the catalogue, not the recommendations.</b> Strategy rows and policy rows
/// are read on every generation and written twice a year, which is the textbook case. A generated
/// page is already persisted as a <c>RecommendationSet</c> with an expiry, and caching it as well
/// would give two answers to "what was this member shown" that could disagree.
/// </para>
/// </summary>
public interface IRecommendationCache
{
    /// <summary>The cached value, or null on a miss. Null is never itself cached.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(
        string key, T value, TimeSpan lifetime, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>The read-through form. What every caller actually wants.</summary>
    Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops everything under a prefix.
    /// <para>
    /// What a catalogue change calls. Without it, retuning a weight would take effect whenever the
    /// entries happened to expire, and an operator watching for the change would see nothing for
    /// minutes and retune again.
    /// </para>
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
