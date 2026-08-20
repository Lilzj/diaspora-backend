using System.Collections.Concurrent;
using Diaspora.Modules.Financial.Extensibility.Caching;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Caching;

/// <summary>
/// The default cache: a concurrent dictionary with expiry, and nothing else.
/// <para>
/// <b>A singleton, and safe as one.</b> No scoped dependency, no shared mutable value handed out —
/// entries are stored and returned by reference, which is correct here because everything cached is
/// an immutable read model.
/// </para>
/// <para>
/// <b>Per-process, and that is a real limitation stated rather than hidden.</b> Two instances hold
/// two caches, so a policy change made on one is visible on the other only after the TTL. The TTL is
/// therefore short and configurable, and a deployment that cares registers a distributed
/// implementation of <c>IFinancialCache</c> — which is the entire reason the port is module-owned
/// rather than the kernel's dormant <c>ICacheService</c>.
/// </para>
/// </summary>
public sealed class InMemoryFinancialCache : IFinancialCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly bool _enabled;

    public InMemoryFinancialCache(IClock clock, IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _enabled = options.Value.EnableCaching;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        if (!_enabled || string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult<T?>(null);
        }

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
        string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
        where T : class
    {
        if (_enabled && !string.IsNullOrWhiteSpace(key) && value is not null)
        {
            _entries[key] = new Entry(value, _clock.UtcNow.Add(ttl));
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Task.CompletedTask;
        }

        foreach (var key in _entries.Keys.Where(key =>
            key.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    private sealed record Entry(object Value, DateTimeOffset ExpiresAt);
}
