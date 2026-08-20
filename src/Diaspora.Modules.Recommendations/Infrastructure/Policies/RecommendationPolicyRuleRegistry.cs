using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Every registered policy rule, indexed by key.
/// <para>
/// The same shape and the same duplicate-key refusal as <c>RankingStrategyRegistry</c>, and for the
/// same reason: two rules claiming one key means one is silently ignored, and a silently ignored
/// policy is content reaching members it should not have.
/// </para>
/// </summary>
public sealed class RecommendationPolicyRuleRegistry
{
    private readonly Dictionary<string, IRecommendationPolicyRule> _byKey;

    public RecommendationPolicyRuleRegistry(IEnumerable<IRecommendationPolicyRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        _byKey = new Dictionary<string, IRecommendationPolicyRule>(StringComparer.Ordinal);

        foreach (var rule in rules)
        {
            var key = rule.Key.Trim().ToLowerInvariant();

            if (_byKey.TryGetValue(key, out var existing))
            {
                throw new InvalidOperationException(
                    $"Two recommendation policy rules claim the key '{key}': "
                    + $"{existing.GetType().FullName} and {rule.GetType().FullName}. One would be "
                    + "silently ignored, and an ignored policy is content reaching a member it "
                    + "should not have.");
            }

            _byKey[key] = rule;
        }

        All = _byKey.Values.ToList();
    }

    public IReadOnlyList<IRecommendationPolicyRule> All { get; }

    public IRecommendationPolicyRule? Find(string key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : _byKey.GetValueOrDefault(key.Trim().ToLowerInvariant());

    public bool IsImplemented(string key) => Find(key) is not null;
}
