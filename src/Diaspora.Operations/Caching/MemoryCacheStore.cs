using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Caching;

/// <summary>
/// The in-process cache tier.
/// <para>
/// Backed by <see cref="IMemoryCache"/> with a hard entry limit, because an unbounded in-process
/// cache is a memory leak with a friendly name. Every entry costs one unit of the limit; the runtime
/// cannot cheaply measure an object graph's real size, and a size limit derived from a guess is a
/// limit nobody can reason about during an incident.
/// </para>
/// <para>
/// <b>Stores bytes, like every other store.</b> Keeping objects here would be faster and would mean
/// a value written locally and a value written to the shared tier could differ — a serialization bug
/// that only appears when a request lands on a replica that did not write the entry, which is the
/// hardest class of bug this platform could give itself.
/// </para>
/// </summary>
public sealed class MemoryCacheStore : ICacheStore, IDisposable
{
    private readonly MemoryCache _cache;
    private readonly IClock _clock;

    public MemoryCacheStore(IOptions<CacheOptions> options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _clock = clock;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = options.Value.LocalEntryLimit,

            // Evict a fifth when the limit is reached. The default (5%) triggers a compaction
            // scan on nearly every write once the cache is full, which costs more than the
            // cache saves.
            CompactionPercentage = 0.2
        });
    }

    public CacheStoreTier Tier => CacheStoreTier.Local;

    public string Provider => "memory";

    public Task<CacheStoreRead> GetAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        if (_cache.TryGetValue(storageKey, out Entry? entry) && entry is not null)
        {
            var now = _clock.UtcNow;

            // Expiry is checked against the platform clock as well as being configured on the entry.
            // Two mechanisms rather than one because they answer different questions:
            // IMemoryCache's own expiry is what evicts and reclaims memory, and it runs on the system
            // clock; this check is what makes "has this expired" a question the platform's own clock
            // answers, which is the only way expiry behaviour can be tested at all.
            if (entry.ExpiresAt > now)
            {
                return Task.FromResult(new CacheStoreRead(
                    CacheSource.Local, entry.Payload, now - entry.WrittenAt));
            }

            _cache.Remove(storageKey);
        }

        return Task.FromResult(CacheStoreRead.Miss);
    }

    public Task SetAsync(
        string storageKey,
        ReadOnlyMemory<byte> value,
        TimeSpan absoluteExpiration,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration,
            SlidingExpiration = slidingExpiration,
            Size = 1
        };

        var now = _clock.UtcNow;

        _cache.Set(storageKey, new Entry(value, now, now + absoluteExpiration), entryOptions);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        cancellationToken.ThrowIfCancellationRequested();

        _cache.Remove(storageKey);

        return Task.CompletedTask;
    }

    /// <summary>Always. Process memory is available exactly as long as the process is.</summary>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public void Dispose() => _cache.Dispose();

    /// <summary>
    /// The payload, its write time and its expiry.
    /// <para>
    /// The write time is what lets the tier above decide that an entry is stale-but-servable, which an
    /// <see cref="IMemoryCache"/> entry cannot report on its own — it either returns the value or has
    /// already dropped it. The expiry is carried for the same reason in reverse: the runtime's own
    /// expiry runs on the system clock, and the platform's notion of "now" is <c>IClock</c>.
    /// </para>
    /// </summary>
    private sealed record Entry(
        ReadOnlyMemory<byte> Payload, DateTimeOffset WrittenAt, DateTimeOffset ExpiresAt);
}
