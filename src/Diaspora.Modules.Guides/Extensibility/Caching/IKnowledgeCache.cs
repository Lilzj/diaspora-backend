namespace Diaspora.Modules.Guides.Extensibility.Caching;

/// <summary>
/// Where composed directory reads are held between requests.
/// <para>
/// <b>An abstraction rather than a cache, and the brief asks for exactly that.</b> A country page
/// is the most expensive read in this module — a directory, its catalogue, seven shelves, a batched
/// guide resolution, a cross-vertical counter row — and it changes when a curator edits it, which
/// is roughly never. That is the ideal shape for caching and the worst possible shape for guessing:
/// a deployment serving one country from one process wants an in-memory dictionary, and one serving
/// forty countries from twelve pods wants Redis with a shared invalidation channel. This interface
/// is what lets the second replace the first without a query handler changing.
/// </para>
/// <para>
/// <b>Invalidation is by scope, not by key, and that is the whole design.</b> A caller that has just
/// arranged a directory cannot enumerate the keys it invalidated — there is one per locale, per
/// category, per limit. It knows only that everything about that country is now suspect, which is
/// precisely what <see cref="InvalidateCountryAsync"/> expresses.
/// </para>
/// <para>
/// <b>Implementations must fail open.</b> A cache that throws when its backing store is unreachable
/// turns a degraded dependency into an outage on the platform's most-read screen. The shipped
/// implementation swallows and misses; any replacement must do the same.
/// </para>
/// </summary>
public interface IKnowledgeCache
{
    /// <summary>The implementation's name, for diagnostics and the capabilities endpoint.</summary>
    string Name { get; }

    /// <summary>Whether this deployment caches at all. False makes every read a miss, by design.</summary>
    bool IsEnabled { get; }

    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    Task SetAsync<T>(
        string key, T value, TimeSpan? timeToLive = null, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Drops everything cached under a prefix. The unit of invalidation.</summary>
    Task InvalidateAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>Drops everything cached for one country. Called on any curation change.</summary>
    Task InvalidateCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the navigation catalogue everywhere.
    /// <para>
    /// Country-wide rather than catalogue-only, because a renamed category changes the label on
    /// every country page that lists it, and there is no cheap way to know which those are.
    /// Renaming a category is rare; serving a stale label on a hundred country pages is not
    /// acceptable at any rate.
    /// </para>
    /// </summary>
    Task InvalidateCatalogueAsync(CancellationToken cancellationToken = default);
}
