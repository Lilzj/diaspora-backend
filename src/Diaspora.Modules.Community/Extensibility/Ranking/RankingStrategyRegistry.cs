namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>Resolves a ranking strategy by key. Duplicate keys fail startup.</summary>
public sealed class RankingStrategyRegistry
{
    private readonly IReadOnlyDictionary<string, IRankingStrategy> _strategies;

    public RankingStrategyRegistry(IEnumerable<IRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        var byKey = new Dictionary<string, IRankingStrategy>(StringComparer.OrdinalIgnoreCase);

        foreach (var strategy in strategies)
        {
            var key = strategy.RankingKey?.Trim();

            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException(
                    $"{strategy.GetType().FullName} declares no ranking key.");
            }

            if (byKey.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two ranking strategies claim '{key}': {existing.GetType().FullName} and " +
                    $"{strategy.GetType().FullName}. A ranking key has exactly one strategy.");
            }

            byKey[key] = strategy;
        }

        _strategies = byKey;
    }

    public IReadOnlyList<string> Keys =>
        _strategies.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList();

    public IRankingStrategy? Find(string? key) =>
        string.IsNullOrWhiteSpace(key) ? null : _strategies.GetValueOrDefault(key.Trim());

    /// <summary>
    /// The named strategy, or chronological.
    /// <para>
    /// Falling back rather than failing, and only here. A feed whose configured ranking has been
    /// unregistered should degrade to newest-first, not 500 — the content is fine, only the
    /// ordering preference is missing.
    /// </para>
    /// </summary>
    public IRankingStrategy Resolve(string? key) =>
        Find(key)
        ?? Find(RankingKeys.Chronological)
        ?? throw new InvalidOperationException(
            "No chronological ranking strategy is registered. The module cannot rank any feed.");
}
