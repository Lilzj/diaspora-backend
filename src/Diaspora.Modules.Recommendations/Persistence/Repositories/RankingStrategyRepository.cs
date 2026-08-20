using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the ranking-strategy catalogue.
/// <para>
/// <b>Every lookup is keyed on the pair, never on the key alone.</b> One implementation is configured
/// on several surfaces with different weights — that is the entire reason weights are data — so a
/// method that took only a key would have to guess which surface's row it meant.
/// </para>
/// </summary>
public sealed class RankingStrategyRepository : IRankingStrategyRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RankingStrategyRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RankingStrategy> Strategies => _dbContext.Set<RankingStrategy>();

    public void Add(RankingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        Strategies.Add(strategy);
    }

    public async Task<RankingStrategy?> FindAsync(
        RankingStrategyId strategyId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strategyId);

        return await Strategies
            .Include(strategy => strategy.Parameters)
            .FirstOrDefaultAsync(strategy => strategy.Id == strategyId, cancellationToken);
    }

    public async Task<RankingStrategy?> FindByKeyAsync(
        string key, RecommendationSurface surface, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = key.Trim().ToLowerInvariant();

        return await Strategies
            .Include(strategy => strategy.Parameters)
            .FirstOrDefaultAsync(
                strategy => strategy.Key == normalized && strategy.Surface == surface,
                cancellationToken);
    }

    public async Task<IReadOnlyList<RankingStrategy>> ListForSurfaceAsync(
        RecommendationSurface surface, CancellationToken cancellationToken = default) =>
        await Strategies
            .Include(strategy => strategy.Parameters)
            .Where(strategy => strategy.Surface == surface && strategy.IsEnabled)
            .OrderBy(strategy => strategy.Order)
            .ThenBy(strategy => strategy.Key)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RankingStrategy>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        await Strategies
            .Include(strategy => strategy.Parameters)
            .OrderBy(strategy => strategy.Surface)
            .ThenBy(strategy => strategy.Order)
            .ThenBy(strategy => strategy.Key)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        string key, RecommendationSurface surface, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(false);
        }

        var normalized = key.Trim().ToLowerInvariant();

        return Strategies.AnyAsync(
            strategy => strategy.Key == normalized && strategy.Surface == surface, cancellationToken);
    }

    public void Update(RankingStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, strategy);
    }
}
