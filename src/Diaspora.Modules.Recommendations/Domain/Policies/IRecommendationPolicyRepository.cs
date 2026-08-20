using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;

namespace Diaspora.Modules.Recommendations.Domain.Policies;

/// <summary>
/// The policy catalogue.
/// <para>
/// Reference data, read on every generation and written by an operator perhaps twice a year. The
/// read is cached behind <c>IRecommendationCache</c>; this port is what the cache misses on.
/// </para>
/// </summary>
public interface IRecommendationPolicyRepository
{
    void Add(RecommendationPolicy policy);

    Task<RecommendationPolicy?> FindAsync(
        RecommendationPolicyId policyId, CancellationToken cancellationToken = default);

    Task<RecommendationPolicy?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>Every enabled policy, in evaluation order. What the evaluator runs.</summary>
    Task<IReadOnlyList<RecommendationPolicy>> ListEnabledAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Every policy including the disabled ones. The administration surface.</summary>
    Task<IReadOnlyList<RecommendationPolicy>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationPolicy>> ListByKindAsync(
        RecommendationPolicyKind kind, CancellationToken cancellationToken = default);

    void Update(RecommendationPolicy policy);
}
