using Diaspora.Modules.Creator.Domain.Recommendations;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>A member's dismissed creator recommendations.</summary>
public sealed class CreatorRecommendationDismissalRepository : ICreatorRecommendationDismissalRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorRecommendationDismissalRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorRecommendationDismissal> Dismissals =>
        _dbContext.Set<CreatorRecommendationDismissal>();

    public void Add(CreatorRecommendationDismissal dismissal) => Dismissals.Add(dismissal);

    public Task<bool> ExistsAsync(
        Guid userId, string recommendationId, CancellationToken cancellationToken = default) =>
        Dismissals.AsNoTracking()
            .AnyAsync(
                dismissal => dismissal.UserId == userId && dismissal.RecommendationId == recommendationId,
                cancellationToken);

    public async Task<IReadOnlyCollection<string>> ListIdsForUserAsync(
        Guid userId, CancellationToken cancellationToken = default) =>
        await Dismissals.AsNoTracking()
            .Where(dismissal => dismissal.UserId == userId)
            .Select(dismissal => dismissal.RecommendationId)
            .ToListAsync(cancellationToken);
}
