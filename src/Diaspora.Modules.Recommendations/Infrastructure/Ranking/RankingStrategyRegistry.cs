using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// Every registered strategy, indexed by key.
/// <para>
/// <b>Fails construction on a duplicate key rather than picking one.</b> Two implementations
/// claiming <c>popularity</c> means one of them is silently ignored, and which one depends on
/// service registration order — so the defect appears when somebody reorders an unrelated
/// registration, months later, with no connection to the change that caused it. A startup failure
/// naming both types costs a minute.
/// </para>
/// <para>
/// Built once per scope from the scanned implementations. The dictionary is ordinal-cased because
/// keys are normalised to lower case everywhere they are written, and a case-insensitive lookup here
/// would hide a catalogue row that disagrees.
/// </para>
/// </summary>
public sealed class RankingStrategyRegistry : IRankingStrategyRegistry
{
    private readonly Dictionary<string, IRankingStrategy> _byKey;

    public RankingStrategyRegistry(IEnumerable<IRankingStrategy> strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        _byKey = new Dictionary<string, IRankingStrategy>(StringComparer.Ordinal);

        foreach (var strategy in strategies)
        {
            var key = strategy.Key.Trim().ToLowerInvariant();

            if (_byKey.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two ranking strategies claim the key '{key}': {existing.GetType().FullName} and "
                    + $"{strategy.GetType().FullName}. One of them would be silently ignored, and which "
                    + "one would depend on registration order.");
            }

            _byKey[key] = strategy;
        }

        All = _byKey.Values.ToList();
    }

    public IReadOnlyList<IRankingStrategy> All { get; }

    public IRankingStrategy? Find(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : _byKey.GetValueOrDefault(key.Trim().ToLowerInvariant());

    public bool IsImplemented(string key) => Find(key) is not null;

    public IReadOnlyList<IRankingStrategy> OfKind(RankingStrategyKind kind) =>
        All.Where(strategy => strategy.Kind == kind).ToList();
}
