using System.Collections.Concurrent;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Observability;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Knowledge;

/// <summary>
/// The cache a single-process deployment gets for free.
/// <para>
/// <b>A dictionary, and it is the right shipped default rather than a placeholder.</b> A composed
/// country page is expensive to build and changes when a curator edits it, which is roughly never —
/// the ideal shape for caching. A deployment running one process gets the whole benefit from an
/// in-memory map with no infrastructure at all; a deployment running twelve pods registers a
/// distributed implementation after this one and replaces it. Nothing above
/// <see cref="IKnowledgeCache"/> changes when it does.
/// </para>
/// <para>
/// <b>Invalidation is by prefix, which is why a dictionary works and a plain memory cache would
/// not.</b> A curator arranging a directory cannot enumerate the keys they invalidated — there is
/// one per locale, per category, per limit. They know only that everything about that country is
/// suspect, and dropping a prefix is the operation that expresses it.
/// </para>
/// <para>
/// <b>A singleton, and safe as one:</b> it holds a concurrent dictionary and no scoped dependency.
/// It is also the one component here whose entire value is that it outlives a request — a
/// per-request cache would cache nothing.
/// </para>
/// </summary>
public sealed class InMemoryKnowledgeCache : IKnowledgeCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly IOptions<KnowledgeOptions> _options;
    private readonly IClock _clock;

    public InMemoryKnowledgeCache(IOptions<KnowledgeOptions> options, IClock clock)
    {
        _options = options;
        _clock = clock;
    }

    public string Name => "in-memory";

    public bool IsEnabled => _options.Value.EnableCaching;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        if (!IsEnabled || !_entries.TryGetValue(key, out var entry))
        {
            KnowledgeTelemetry.CacheMisses.Add(1);
            return Task.FromResult<T?>(null);
        }

        if (entry.ExpiresAt <= _clock.UtcNow)
        {
            _entries.TryRemove(key, out _);
            KnowledgeTelemetry.CacheMisses.Add(1);

            return Task.FromResult<T?>(null);
        }

        KnowledgeTelemetry.CacheHits.Add(1);

        return Task.FromResult(entry.Value as T);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? timeToLive = null,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        Evict();

        _entries[key] = new Entry(
            value, _clock.UtcNow.Add(timeToLive ?? _options.Value.CacheDuration));

        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string keyPrefix, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(keyPrefix))
        {
            return Task.CompletedTask;
        }

        foreach (var key in _entries.Keys.Where(
            key => key.StartsWith(keyPrefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task InvalidateCountryAsync(
        string countryCode, CancellationToken cancellationToken = default) =>
        InvalidateAsync(KnowledgeCacheKeys.CountryScope(countryCode), cancellationToken);

    /// <summary>
    /// Drops the catalogue <em>and</em> every country page.
    /// <para>
    /// Wider than it looks, and deliberately. A renamed navigation row changes the label on every
    /// country page that lists it, and there is no cheap way to know which those are. Renaming a
    /// category is rare; serving a stale label on a hundred country pages is not acceptable at any
    /// rate.
    /// </para>
    /// </summary>
    public async Task InvalidateCatalogueAsync(CancellationToken cancellationToken = default)
    {
        await InvalidateAsync(KnowledgeCacheKeys.CataloguePrefix, cancellationToken);
        await InvalidateAsync(KnowledgeCacheKeys.CountryPrefix, cancellationToken);
    }

    /// <summary>
    /// Keeps the map bounded by dropping expired entries first, then the oldest.
    /// <para>
    /// Crude, and correct for what this is: a bounded map in front of an expensive read, where an
    /// eviction costs one rebuild. A deployment that needs an eviction policy worth naming has
    /// outgrown the in-memory implementation and should register the distributed one.
    /// </para>
    /// </summary>
    private void Evict()
    {
        var capacity = Math.Max(16, _options.Value.CacheCapacity);

        if (_entries.Count < capacity)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var expired in _entries
            .Where(entry => entry.Value.ExpiresAt <= now)
            .Select(entry => entry.Key)
            .ToList())
        {
            _entries.TryRemove(expired, out _);
        }

        if (_entries.Count < capacity)
        {
            return;
        }

        foreach (var oldest in _entries
            .OrderBy(entry => entry.Value.ExpiresAt)
            .Take(_entries.Count - capacity + 1)
            .Select(entry => entry.Key)
            .ToList())
        {
            _entries.TryRemove(oldest, out _);
        }
    }

    private sealed record Entry(object Value, DateTimeOffset ExpiresAt);
}
