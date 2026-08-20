using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>EF Core repository for digests.</summary>
public sealed class NotificationDigestRepository : INotificationDigestRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NotificationDigestRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NotificationDigest> Digests => _dbContext.Set<NotificationDigest>();

    public async Task<NotificationDigest?> GetAsync(
        NotificationDigestId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Digests
            .Include(digest => digest.Entries)
            .FirstOrDefaultAsync(digest => digest.Id == id, cancellationToken);
    }

    public async Task<CursorPagedResult<NotificationDigest>> ListForUserAsync(
        UserId userId, string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var bounded = Math.Clamp(limit, 1, CursorPaginationRequest.MaxLimit);

        var query = Digests
            .AsNoTracking()
            .Include(digest => digest.Entries)
            .Where(digest => digest.RecipientUserId == userId);

        var parsed = NotificationCursor.Parse(cursor);
        var boundary = parsed?.AsTimestamp();

        if (boundary is { } after)
        {
            var exclusion = CursorBoundary.Exclusion(parsed!.LastKey, NotificationDigestId.From);

            query = exclusion.IsExhausted
                ? query.Where(digest => digest.GeneratedAt < after)
                : query.Where(digest =>
                    digest.GeneratedAt < after
                    || (digest.GeneratedAt == after && !exclusion.Ids.Contains(digest.Id)));
        }

        var page = await query
            .OrderByDescending(digest => digest.GeneratedAt)
            .ThenByDescending(digest => digest.Id)
            .Take(bounded + 1)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > bounded;
        var items = hasMore ? page.Take(bounded).ToList() : page;

        if (items.Count == 0)
        {
            return CursorPagedResult<NotificationDigest>.Empty();
        }

        var boundaryKey = CursorBoundary.Key(
            items,
            digest => digest.GeneratedAt,
            digest => digest.Id.Value,
            parsed?.LastKey,
            boundary);

        var next = hasMore
            ? NotificationCursor.FromTimestamp(items[^1].GeneratedAt, boundaryKey).Encode()
            : null;

        return CursorPagedResult<NotificationDigest>.Create(items, next, hasMore);
    }

    public Task<bool> ExistsForWindowAsync(
        UserId userId,
        DigestFrequency frequency,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string? categoryKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var key = string.IsNullOrWhiteSpace(categoryKey) ? null : categoryKey.Trim().ToLowerInvariant();

        return Digests
            .AsNoTracking()
            .AnyAsync(
                digest => digest.RecipientUserId == userId
                    && digest.Frequency == frequency
                    && digest.WindowStart == windowStart
                    && digest.WindowEnd == windowEnd
                    && digest.CategoryKey == key,
                cancellationToken);
    }

    public async Task AddAsync(
        NotificationDigest digest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(digest);

        await Digests.AddAsync(digest, cancellationToken);
    }

    public void Update(NotificationDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        NotificationAggregateUpdater.MarkDirty(_dbContext, digest);
    }
}
