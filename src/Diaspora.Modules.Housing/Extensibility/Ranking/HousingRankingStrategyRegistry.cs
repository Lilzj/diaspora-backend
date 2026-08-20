using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Extensibility.Ranking;

/// <summary>
/// Resolves a ranking key to the strategy that owns it.
/// <para>
/// <b>Scoped, not singleton.</b> Every real strategy will eventually hold a repository or a client,
/// and a singleton registry over scoped implementations is the captive dependency the host's
/// provider validation refuses to start on. B4 recorded that failure first and B6, B7, B8, B9 and
/// B10 each repeated the note; paying one dictionary construction per request is a small price for
/// not repeating it a seventh time.
/// </para>
/// <para>
/// An unknown key falls back rather than failing. A stale client sending a ranking that has been
/// retired should get results in a sensible order, not an error on a browse screen.
/// </para>
/// </summary>
public sealed class HousingRankingStrategyRegistry
{
    private readonly Dictionary<string, IHousingRankingStrategy> _strategies;

    public HousingRankingStrategyRegistry(IEnumerable<IHousingRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = strategies
            .GroupBy(strategy => strategy.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every key registered. For the documentation and the client's sort picker.</summary>
    public IReadOnlyCollection<string> Keys => _strategies.Keys;

    /// <summary>
    /// Ranks a page with whichever strategy owns the key.
    /// <para>
    /// An unrecognised key, or none at all, returns the page in the order the repository produced
    /// it — which is already a deliberate ordering, not an accident.
    /// </para>
    /// </summary>
    public IReadOnlyList<HousingListing> Rank(
        string? key, IReadOnlyList<HousingListing> listings, HousingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(listings);

        if (string.IsNullOrWhiteSpace(key) || listings.Count <= 1)
        {
            return listings;
        }

        return _strategies.TryGetValue(key, out var strategy)
            ? strategy.Rank(listings, context)
            : listings;
    }
}
