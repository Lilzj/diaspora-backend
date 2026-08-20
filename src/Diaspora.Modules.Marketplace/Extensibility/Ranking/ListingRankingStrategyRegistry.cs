using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Marketplace.Extensibility.Ranking;

/// <summary>
/// Resolves a ranking strategy by key.
/// <para>
/// <b>Scoped, not singleton, and that is load-bearing.</b> A contributed strategy may hold a
/// repository or an HTTP client, and a singleton registry over scoped implementations is the
/// captive dependency the host's provider validation refuses to start on. B4 recorded that failure
/// with its country catalogue and B6, B7, B8 and B9 each repeated the note; paying one dictionary
/// construction per request is a small price for not repeating it a sixth time.
/// </para>
/// <para>
/// An unknown key resolves to the default rather than failing. A client asking for a strategy this
/// deployment does not ship should get results, not an error page.
/// </para>
/// </summary>
public sealed class ListingRankingStrategyRegistry
{
    private readonly Dictionary<string, IListingRankingStrategy> _strategies;
    private readonly IListingRankingStrategy? _fallback;

    public ListingRankingStrategyRegistry(
        IEnumerable<IListingRankingStrategy> strategies,
        ILogger<ListingRankingStrategyRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(logger);

        _strategies = new Dictionary<string, IListingRankingStrategy>(StringComparer.OrdinalIgnoreCase);

        foreach (var strategy in strategies)
        {
            if (_strategies.TryGetValue(strategy.Key, out var existing))
            {
                logger.LogWarning(
                    "Ranking key '{Key}' is claimed by both {Existing} and {Replacement}. The "
                    + "later registration wins.",
                    strategy.Key,
                    existing.GetType().Name,
                    strategy.GetType().Name);
            }

            _strategies[strategy.Key] = strategy;
        }

        _fallback = _strategies.GetValueOrDefault(ListingRankingKeys.Balanced)
            ?? _strategies.Values.FirstOrDefault();
    }

    /// <summary>Every strategy this deployment offers. For the settings screen.</summary>
    public IReadOnlyCollection<IListingRankingStrategy> All => _strategies.Values;

    /// <summary>
    /// The strategy for a key, or the default. Null only when the deployment registered none at
    /// all, which the caller treats as "leave the order alone".
    /// </summary>
    public IListingRankingStrategy? Resolve(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? _fallback
            : _strategies.GetValueOrDefault(key) ?? _fallback;
}
