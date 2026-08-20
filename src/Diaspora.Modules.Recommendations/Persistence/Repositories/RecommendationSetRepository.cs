using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for generated pages.
/// <para>
/// <b>Every read includes the items and their contributions, and that is three levels of graph on
/// purpose.</b> A set without its items is a row nobody can render, and a set whose items have no
/// contributions cannot answer "why" — which is the entire reason the table exists.
/// </para>
/// </summary>
public sealed class RecommendationSetRepository : IRecommendationSetRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RecommendationSetRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RecommendationSet> Sets => _dbContext.Set<RecommendationSet>();

    private IQueryable<RecommendationSet> WithGraph =>
        Sets.Include(set => set.Items).ThenInclude(item => item.Contributions);

    public void Add(RecommendationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        Sets.Add(set);
    }

    public async Task<RecommendationSet?> FindAsync(
        RecommendationSetId setId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setId);

        return await WithGraph.FirstOrDefaultAsync(set => set.Id == setId, cancellationToken);
    }

    /// <summary>
    /// The member's current page for a surface.
    /// <para>
    /// Ordered newest first and taking one, because a member can have several unexpired sets for a
    /// surface if generation was forced — and the one they were last shown is the one "why am I
    /// seeing this" is about.
    /// </para>
    /// </summary>
    public async Task<RecommendationSet?> FindFreshAsync(
        UserId userId,
        RecommendationSurface surface,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await WithGraph
            .Where(set => set.UserId == userId && set.Surface == surface && set.ExpiresAt > asOf)
            .OrderByDescending(set => set.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RecommendationSet?> FindMostRecentContainingAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await WithGraph
            .Where(set => set.UserId == userId)
            .Where(set => set.Items.Any(item =>
                item.Target.TargetType == targetType && item.Target.TargetId == targetId))
            .OrderByDescending(set => set.GeneratedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationSet>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await WithGraph
            .Where(set => set.UserId == userId)
            .OrderByDescending(set => set.GeneratedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecommendationSet>> ListGeneratedBetweenAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken = default) =>
        await Sets
            .Where(set => set.GeneratedAt >= from && set.GeneratedAt < to)
            .OrderBy(set => set.GeneratedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public void Update(RecommendationSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, set);
    }

    /// <summary>
    /// Retention.
    /// <para>
    /// <c>IgnoreQueryFilters</c> because the set is soft-deletable and the global filter hides
    /// exactly the rows a retention pass exists to find — the blind spot this platform has recorded
    /// before.
    /// </para>
    /// </summary>
    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset generatedBefore, int batchSize, CancellationToken cancellationToken = default)
    {
        var doomed = await Sets
            .IgnoreQueryFilters()
            .Include(set => set.Items).ThenInclude(item => item.Contributions)
            .Where(set => set.GeneratedAt < generatedBefore)
            .OrderBy(set => set.GeneratedAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Sets.RemoveRange(doomed);

        return doomed.Count;
    }

    public async Task<int> DeleteForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var doomed = await Sets
            .IgnoreQueryFilters()
            .Include(set => set.Items).ThenInclude(item => item.Contributions)
            .Where(set => set.UserId == userId)
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Sets.RemoveRange(doomed);

        return doomed.Count;
    }
}
