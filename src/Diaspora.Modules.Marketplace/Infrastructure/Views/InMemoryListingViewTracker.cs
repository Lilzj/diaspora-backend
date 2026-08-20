using System.Collections.Concurrent;
using Diaspora.Modules.Marketplace.Extensibility.Views;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Views;

/// <summary>
/// De-duplicates views in this process, for a window.
/// <para>
/// <b>The brief asks for unique views, and this is the only honest way to get them without a row
/// per view.</b> Storing every view would cost a table growing by millions a week that has to be
/// pruned forever, in exchange for answering questions nobody asks. A cache entry per (listing,
/// viewer) that expires is exactly what "unique in a window" means.
/// </para>
/// <para>
/// <b>Approximate on several instances, and deliberately so.</b> A listing viewed once per instance
/// counts as a handful of unique views rather than one — which overstates a number nobody makes a
/// decision on, by a factor bounded by the instance count. A deployment that cares registers a
/// distributed implementation of <see cref="IListingViewTracker"/> and nothing above the interface
/// changes. Paying for Redis to make a view counter exactly right is not a trade this platform
/// should make by default.
/// </para>
/// <para>
/// <b>Nothing is persisted and nothing outlives the window.</b> The viewer key is held for as long
/// as the de-duplication lasts and no longer, which is what keeps "unique views" from becoming a
/// log of who looked at what — the most behaviourally revealing thing this module could
/// accidentally start keeping.
/// </para>
/// <para>
/// <b>A singleton, and safe as one:</b> it holds no scoped dependency, only a concurrent dictionary
/// and the clock.
/// </para>
/// </summary>
public sealed class InMemoryListingViewTracker : IListingViewTracker
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly IClock _clock;
    private readonly IOptions<MarketplaceOptions> _options;

    private DateTimeOffset _lastPrune;

    public InMemoryListingViewTracker(IClock clock, IOptions<MarketplaceOptions> options)
    {
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Records a view and says whether it was the viewer's first in the window.
    /// <para>
    /// Never throws. A tracker that failed would fail the page render of a listing, and a view count
    /// is not worth a listing nobody can open — so an unusable key answers false, which undercounts
    /// rather than breaking.
    /// </para>
    /// </summary>
    public Task<bool> RegisterViewAsync(
        Guid listingId, string viewerKey, CancellationToken cancellationToken = default)
    {
        if (listingId == Guid.Empty || string.IsNullOrWhiteSpace(viewerKey))
        {
            return Task.FromResult(false);
        }

        var now = _clock.UtcNow;
        var window = TimeSpan.FromMinutes(Math.Max(1, _options.Value.UniqueViewWindowMinutes));
        var key = $"{listingId:N}:{viewerKey}";

        Prune(now, window);

        var isFirst = true;

        _seen.AddOrUpdate(
            key,
            now,
            (_, seenAt) =>
            {
                if (now - seenAt < window)
                {
                    isFirst = false;
                    return seenAt;
                }

                return now;
            });

        return Task.FromResult(isFirst);
    }

    /// <summary>
    /// Drops expired entries, at most once per window and only once the dictionary has grown past
    /// its ceiling.
    /// <para>
    /// Bounded work on an unbounded structure: without it the dictionary grows for the life of the
    /// process, and with it on every call the sweep would cost more than the counting does. When
    /// pruning cannot get the size down — every entry still inside its window — the whole thing is
    /// cleared, which loses de-duplication for one window rather than leaking memory forever.
    /// </para>
    /// </summary>
    private void Prune(DateTimeOffset now, TimeSpan window)
    {
        var capacity = Math.Max(1_000, _options.Value.ViewTrackerCapacity);

        if (_seen.Count < capacity || now - _lastPrune < window)
        {
            return;
        }

        _lastPrune = now;

        foreach (var entry in _seen)
        {
            if (now - entry.Value >= window)
            {
                _seen.TryRemove(entry.Key, out _);
            }
        }

        if (_seen.Count >= capacity)
        {
            _seen.Clear();
        }
    }
}
