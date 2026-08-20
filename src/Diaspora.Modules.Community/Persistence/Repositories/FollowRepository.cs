using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for the follow graph.</summary>
public sealed class FollowRepository : IFollowRepository
{
    private readonly DiasporaDbContext _dbContext;

    public FollowRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Follow> Follows => _dbContext.Set<Follow>();

    public async Task<Follow?> FindAsync(
        UserId followerId,
        FollowTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var key = Follow.BuildKey(followerId, targetType, targetId);

        return await Follows.FirstOrDefaultAsync(
            follow => follow.EdgeKey == key, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListTargetsAsync(
        UserId followerId,
        FollowTargetType targetType,
        bool includeMuted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(followerId);

        var query = Follows.Where(follow =>
            follow.FollowerId == followerId && follow.TargetType == targetType);

        if (!includeMuted)
        {
            query = query.Where(follow => !follow.IsMuted);
        }

        return await query.Select(follow => follow.TargetId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListFollowersAsync(
        FollowTargetType targetType,
        Guid targetId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await Follows
            .Where(follow => follow.TargetType == targetType && follow.TargetId == targetId)
            .OrderByDescending(follow => follow.FollowedAt)
            .Take(limit)
            .Select(follow => follow.FollowerId.Value)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Ascending by the moment they followed, so paging is stable.
    /// <para>
    /// See the interface's note: a descending sort shifts under a follow gained mid-walk, and the
    /// consequence for the caller that motivated this — an automation broadcasting to an audience —
    /// is a member skipped and a member notified twice.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        await Follows
            .Where(follow => follow.TargetType == targetType && follow.TargetId == targetId)
            .OrderBy(follow => follow.FollowedAt)
            .ThenBy(follow => follow.Id)
            .Skip(skip)
            .Take(take)
            .Select(follow => follow.FollowerId.Value)
            .ToListAsync(cancellationToken);

    public async Task<int> CountFollowersAsync(
        FollowTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) =>
        await Follows.CountAsync(
            follow => follow.TargetType == targetType && follow.TargetId == targetId,
            cancellationToken);

    public async Task<int> CountFollowingAsync(
        UserId followerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(followerId);

        return await Follows.CountAsync(
            follow => follow.FollowerId == followerId, cancellationToken);
    }

    public async Task<IReadOnlySet<Guid>> FilterFollowedAsync(
        UserId followerId,
        FollowTargetType targetType,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(followerId);
        ArgumentNullException.ThrowIfNull(targetIds);

        if (targetIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var found = await Follows
            .Where(follow =>
                follow.FollowerId == followerId &&
                follow.TargetType == targetType &&
                targetIds.Contains(follow.TargetId))
            .Select(follow => follow.TargetId)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public async Task AddAsync(Follow follow, CancellationToken cancellationToken = default) =>
        await Follows.AddAsync(follow, cancellationToken);

    public void Remove(Follow follow) => Follows.Remove(follow);
}
