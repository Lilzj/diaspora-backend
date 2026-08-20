namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// Resolves a ranking strategy by key.
/// <para>
/// Two strategies claiming the same key fails construction rather than letting a race decide
/// which one answers — the same rule every other registry on this platform follows, and for
/// the same reason: a race decides differently in production than in test, and the symptom is
/// a browse order nobody can reproduce.
/// </para>
/// <para>
/// Scoped, like every registry in this module: nearly every real strategy will eventually hold
/// a repository or a client with per-request state, and a singleton registry over scoped
/// implementations is the captive dependency the host's provider validation refuses to start
/// on. Constructing one dictionary per request is a small price for not repeating B4's lesson.
/// </para>
/// </summary>
public sealed class GuideRankingStrategyRegistry
{
    private readonly Dictionary<string, IGuideRankingStrategy> _strategies;

    public GuideRankingStrategyRegistry(IEnumerable<IGuideRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _strategies = new Dictionary<string, IGuideRankingStrategy>(StringComparer.OrdinalIgnoreCase);

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
    /// The strategy for a key, falling back to <see cref="GuideRankingKeys.Newest"/>.
    /// <para>
    /// Falls back rather than failing, because a client that saved a ranking key which a later
    /// deployment removed would otherwise get an error on a browse screen — and a browse screen
    /// that errors is worse than one ordered differently from what was asked.
    /// </para>
    /// </summary>
    public IGuideRankingStrategy Resolve(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && _strategies.TryGetValue(key, out var strategy))
        {
            return strategy;
        }

        return _strategies.TryGetValue(GuideRankingKeys.Newest, out var fallback)
            ? fallback
            : _strategies.Values.First();
    }
}
