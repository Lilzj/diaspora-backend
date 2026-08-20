using System.Collections.Concurrent;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Recommendations.Infrastructure.Caching;

/// <summary>
/// A cache that works, in one process.
/// <para>
/// <b>Shipped rather than left as an abstraction, and B13 is why.</b> The kernel declares
/// <c>ICacheService</c> and nothing implements it; depending on it makes a component fail silently.
/// This module declares its own seam and ships a working implementation behind it, so the caching in
/// the policy evaluator and the strategy catalogue works on day one and swapping in Redis is a
/// registration change rather than an enablement.
/// </para>
/// <para>
/// <b>Single-process, and honest about it.</b> On a multi-instance deployment each instance caches
/// independently, so a catalogue change takes up to the cache lifetime to reach every instance. That
/// is acceptable for reference data that changes twice a year and unacceptable for anything else,
/// which is why nothing else is cached here.
/// </para>
/// <para>
/// <b>Expiry is checked on read rather than swept.</b> A background sweeper for a dictionary holding
/// a handful of catalogue entries would be a thread and a timer bought for nothing; the entries are
/// overwritten on the next miss.
/// </para>
/// </summary>
public sealed class InMemoryRecommendationCache : IRecommendationCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    private readonly IClock _clock;

    public InMemoryRecommendationCache(IClock clock) => _clock = clock;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_entries.TryGetValue(key, out var entry))
        {
            return Task.FromResult<T?>(null);
        }

        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return Task.FromResult<T?>(null);
        }

        return Task.FromResult(entry.Value as T);
    }

    public Task SetAsync<T>(
        string key, T value, TimeSpan lifetime, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _entries[key] = new CacheEntry(value, _clock.UtcNow.Add(lifetime));

        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan lifetime,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (await GetAsync<T>(key, cancellationToken) is { } cached)
        {
            return cached;
        }

        // Deliberately not locked. Two concurrent misses run the factory twice and the second
        // overwrites the first — which for a read-only catalogue query costs one extra read and
        // avoids holding a lock across an await, which is the shape that deadlocks.
        var created = await factory(cancellationToken);

        await SetAsync(key, created, lifetime, cancellationToken);

        return created;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        foreach (var key in _entries.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private sealed record CacheEntry(object Value, DateTimeOffset ExpiresAt);
}
