namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// Resolves a ranking strategy by key.
/// <para>
/// Two strategies claiming the same key fails startup rather than letting a race decide which
/// one answers — the same rule every other registry on this platform follows, and for the same
/// reason: a race decides differently in production than it does in test, and the symptom is a
/// search order nobody can reproduce.
/// </para>
/// <para>
/// Scoped, like every registry in this module. See
/// <c>ProviderVerificationProviderRegistry</c> for the captive-dependency reasoning.
/// </para>
/// </summary>
public sealed class RankingStrategyRegistry
{
    private readonly Dictionary<string, IProviderRankingStrategy> _strategies;

    public RankingStrategyRegistry(IEnumerable<IProviderRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = new Dictionary<string, IProviderRankingStrategy>(StringComparer.OrdinalIgnoreCase);

        foreach (var strategy in strategies)
        {
            if (!_strategies.TryAdd(strategy.Key, strategy))
            {
                throw new InvalidOperationException(
                    $"Two ranking strategies claim the key '{strategy.Key}': " +
                    $"'{_strategies[strategy.Key].Module}' and '{strategy.Module}'. " +
                    "A ranking key must have exactly one owner.");
            }
        }
    }

    /// <summary>Every registered key, for the capabilities endpoint a client bootstraps from.</summary>
    public IReadOnlyCollection<string> Keys => _strategies.Keys;

    /// <summary>Whether a key is available on this deployment.</summary>
    public bool Contains(string key) => _strategies.ContainsKey(key);

    /// <summary>
    /// The strategy for a key, falling back to <see cref="RankingKeys.Balanced"/>.
    /// <para>
    /// Falls back rather than failing, because a client that saved a ranking key which a later
    /// deployment removed would otherwise get an error on a browse screen — and a browse screen
    /// that errors is worse than one ordered differently from what was asked.
    /// </para>
    /// </summary>
    public IProviderRankingStrategy Resolve(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && _strategies.TryGetValue(key, out var strategy))
        {
            return strategy;
        }

        return _strategies.TryGetValue(RankingKeys.Balanced, out var fallback)
            ? fallback
            : _strategies.Values.First();
    }
}
