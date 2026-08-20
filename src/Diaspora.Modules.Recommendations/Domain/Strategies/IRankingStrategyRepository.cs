using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;

namespace Diaspora.Modules.Recommendations.Domain.Strategies;

/// <summary>
/// The ranking-strategy catalogue.
/// <para>
/// <b><see cref="ListForSurfaceAsync"/> is the read that defines what a surface is.</b> Two surfaces
/// with identical candidate pools and different rows here produce entirely different pages, which is
/// the point of making weights data.
/// </para>
/// </summary>
public interface IRankingStrategyRepository
{
    void Add(RankingStrategy strategy);

    Task<RankingStrategy?> FindAsync(
        RankingStrategyId strategyId, CancellationToken cancellationToken = default);

    Task<RankingStrategy?> FindByKeyAsync(
        string key, RecommendationSurface surface, CancellationToken cancellationToken = default);

    /// <summary>The enabled strategies for a surface, in order. See the class summary.</summary>
    Task<IReadOnlyList<RankingStrategy>> ListForSurfaceAsync(
        RecommendationSurface surface, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RankingStrategy>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string key, RecommendationSurface surface, CancellationToken cancellationToken = default);

    void Update(RankingStrategy strategy);
}
