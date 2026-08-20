using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Notifications.Persistence.Repositories;

/// <summary>
/// EF Core repository for notifications.
/// <para>
/// <b>Every read here is scoped by the recipient before anything else.</b> Not as an optimisation —
/// as the access control. A notification belongs to exactly one member and there is no shared, public
/// or moderated read of one, so a query that could return another member's message is a defect
/// regardless of what the handler above it checks.
/// </para>
/// <para>
/// <b>Paging excludes by identity at the boundary rather than comparing on it.</b> Identifiers are
/// value-converted strongly typed ids and no provider translates an ordering comparison on one, so
/// the textbook keyset predicate cannot be written. Messages created in a single unit of work share
/// a timestamp exactly, so a strict comparison would silently drop all but one of them — which on an
/// inbox means the member never sees a message and has no way to know.
/// </para>
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private static readonly NotificationStatus[] ArchivedStatuses =
    [
        NotificationStatus.Archived,
        NotificationStatus.Expired
    ];

    private static readonly NotificationStatus[] UnreadStatuses =
    [
        NotificationStatus.Created,
        NotificationStatus.Queued,
        NotificationStatus.Delivered
    ];

    private readonly DiasporaDbContext _dbContext;

    public NotificationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Notification> Notifications => _dbContext.Set<Notification>();

    public async Task<Notification?> GetAsync(
        NotificationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Notifications
            .Include(notification => notification.Deliveries)
            .Include(notification => notification.Actions)
            .FirstOrDefaultAsync(notification => notification.Id == id, cancellationToken);
    }

    /// <summary>
    /// Loads a soft-deleted message.
    /// <para>
    /// The global query filter hides exactly the row an undelete needs, which is the trap B1.0.1
    /// recorded and every module since has re-learned. One named method, so no handler has to
    /// remember to disable the filter.
    /// </para>
    /// </summary>
    public async Task<Notification?> GetIncludingDeletedAsync(
        NotificationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Notifications
            .IgnoreQueryFilters()
            .Include(notification => notification.Deliveries)
            .Include(notification => notification.Actions)
            .FirstOrDefaultAsync(notification => notification.Id == id, cancellationToken);
    }

    public async Task<CursorPagedResult<Notification>> SearchAsync(
        NotificationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == filter.RecipientUserId);

        query = ApplyScope(query, filter);
        query = ApplyFilters(query, filter);

        var limit = Math.Clamp(
            filter.Limit ?? CursorPaginationRequest.DefaultLimit, 1, CursorPaginationRequest.MaxLimit);

        var cursor = NotificationCursor.Parse(filter.Cursor);
        var boundary = cursor?.AsTimestamp();

        if (boundary is { } after)
        {
            var exclusion = CursorBoundary.Exclusion(cursor!.LastKey, NotificationId.From);

            query = exclusion.IsExhausted
                ? query.Where(notification => notification.CreatedAt < after)
                : query.Where(notification =>
                    notification.CreatedAt < after
                    || (notification.CreatedAt == after && !exclusion.Ids.Contains(notification.Id)));
        }

        // Pinned first, then newest. The pin is a position the member chose and outranks recency,
        // which is the only reason to have one.
        var page = await query
            .OrderByDescending(notification => notification.IsPinned)
            .ThenByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(limit + 1)
            .Include(notification => notification.Actions)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = hasMore ? page.Take(limit).ToList() : page;

        if (items.Count == 0)
        {
            return CursorPagedResult<Notification>.Empty();
        }

        var boundaryKey = CursorBoundary.Key(
            items,
            notification => notification.CreatedAt,
            notification => notification.Id.Value,
            cursor?.LastKey,
            boundary);

        var next = hasMore
            ? NotificationCursor.FromTimestamp(items[^1].CreatedAt, boundaryKey).Encode()
            : null;

        return CursorPagedResult<Notification>.Create(items, next, hasMore);
    }

    /// <summary>
    /// The scope, which is not expressible as a status list.
    /// <para>
    /// "Inbox" means live and not archived; "archived" means archived <em>or</em> expired. Making a
    /// client enumerate the statuses would freeze the lifecycle into the wire contract, so the
    /// vocabulary is deliberately coarse and translated here.
    /// </para>
    /// </summary>
    private static IQueryable<Notification> ApplyScope(
        IQueryable<Notification> query, NotificationQueryFilter filter) =>
        filter.Scope?.ToLowerInvariant() switch
        {
            "archived" => query.Where(notification => ArchivedStatuses.Contains(notification.Status)),
            "starred" => query.Where(notification => notification.IsStarred),
            "pinned" => query.Where(notification => notification.IsPinned),
            "all" => query,
            _ => query.Where(notification => !ArchivedStatuses.Contains(notification.Status))
        };

    private static IQueryable<Notification> ApplyFilters(
        IQueryable<Notification> query, NotificationQueryFilter filter)
    {
        if (filter.UnreadOnly == true)
        {
            query = query.Where(notification => UnreadStatuses.Contains(notification.Status));
        }

        if (filter.CategoryKeys is { Count: > 0 })
        {
            var keys = filter.CategoryKeys
                .Select(key => key.Trim().ToLowerInvariant())
                .ToList();

            query = query.Where(notification => keys.Contains(notification.CategoryKey));
        }

        if (filter.Priorities is { Count: > 0 })
        {
            var priorities = filter.Priorities.ToList();
            query = query.Where(notification => priorities.Contains(notification.Priority));
        }

        if (!string.IsNullOrWhiteSpace(filter.SourceModule))
        {
            var module = filter.SourceModule.Trim();
            query = query.Where(notification => notification.Source.Module == module);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            var entityType = filter.EntityType.Trim().ToLowerInvariant();
            query = query.Where(notification =>
                notification.PrimaryLink != null && notification.PrimaryLink.EntityType == entityType);
        }

        if (filter.CreatedAfter is { } from)
        {
            query = query.Where(notification => notification.CreatedAt >= from);
        }

        if (filter.CreatedBefore is { } to)
        {
            query = query.Where(notification => notification.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Case-insensitive containment over the frozen content. Adequate for a member's own
            // inbox, which is a bounded set behind an indexed identifier rather than a corpus — and
            // the reason INotificationSearchEngine exists is the day that stops being true.
            var term = filter.Search.Trim();

            query = query.Where(notification =>
                EF.Functions.Like(notification.Content.Title, $"%{term}%")
                || EF.Functions.Like(notification.Content.Body, $"%{term}%"));
        }

        return query;
    }

    public Task<bool> ExistsRecentlyAsync(
        UserId recipientUserId,
        string templateKey,
        string deduplicationKey,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        var key = templateKey.Trim().ToLowerInvariant();

        return Notifications
            .AsNoTracking()
            .AnyAsync(
                notification => notification.RecipientUserId == recipientUserId
                    && notification.TemplateKey == key
                    && notification.GroupKey == deduplicationKey
                    && notification.CreatedAt >= since,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> ListForDigestAsync(
        UserId recipientUserId,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        string? categoryKey,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        var query = Notifications
            .AsNoTracking()
            .Include(notification => notification.Deliveries)
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .Where(notification => notification.CreatedAt >= windowStart)
            .Where(notification => notification.CreatedAt < windowEnd);

        if (!string.IsNullOrWhiteSpace(categoryKey))
        {
            var key = categoryKey.Trim().ToLowerInvariant();
            query = query.Where(notification => notification.CategoryKey == key);
        }

        return await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Deliveries the dispatcher should act on now, across every member.
    /// <para>
    /// Filtered on the owned delivery rows rather than on the notification's own status, because a
    /// message can be delivered on one channel and still due on another — and a query keyed on the
    /// aggregate's status would stop retrying push the moment the inbox row succeeded.
    /// </para>
    /// <para>
    /// Ordered by the due time so the oldest work drains first. A sweep that took whatever the
    /// database returned would starve the messages that had been waiting longest, which are by
    /// definition the ones already late.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Notification>> ListDueForDeliveryAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Notifications
            .Include(notification => notification.Deliveries)
            .Include(notification => notification.Actions)
            .Where(notification => notification.Deliveries.Any(delivery =>
                delivery.NextAttemptAt != null
                && delivery.NextAttemptAt <= asOf
                && (delivery.Status == DeliveryStatus.Pending
                    || delivery.Status == DeliveryStatus.Deferred
                    || delivery.Status == DeliveryStatus.Retrying)))
            .OrderBy(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Notifications
            .Where(notification => notification.ExpiresAt != null && notification.ExpiresAt <= asOf)
            .Where(notification => notification.Status != NotificationStatus.Expired)
            .OrderBy(notification => notification.ExpiresAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadAsync(
        UserId recipientUserId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        return await Notifications
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .Where(notification => UnreadStatuses.Contains(notification.Status))
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        UserId recipientUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        return Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == recipientUserId
                    && UnreadStatuses.Contains(notification.Status),
                cancellationToken);
    }

    public Task<int> CountPinnedAsync(
        UserId recipientUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        return Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == recipientUserId
                    && notification.IsPinned,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> ListByChannelStatusAsync(
        UserId recipientUserId,
        NotificationChannelKind channel,
        DeliveryStatus status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        return await Notifications
            .AsNoTracking()
            .Include(notification => notification.Deliveries)
            .Where(notification => notification.RecipientUserId == recipientUserId)
            .Where(notification => notification.Deliveries.Any(delivery =>
                delivery.Channel == channel && delivery.Status == status))
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The measured truth the reconciliation sweep compares the counters against.
    /// <para>
    /// Five aggregate queries and one grouping, deliberately — not one query returning rows the
    /// caller counts in memory. A member with two years of history would otherwise be materialised
    /// in full to repair a number.
    /// </para>
    /// </summary>
    public async Task<InboxCountSnapshot> CountForReconciliationAsync(
        UserId recipientUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientUserId);

        var mine = Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == recipientUserId);

        var unread = await mine
            .CountAsync(notification => UnreadStatuses.Contains(notification.Status), cancellationToken);

        var total = await mine
            .CountAsync(
                notification => !ArchivedStatuses.Contains(notification.Status), cancellationToken);

        var archived = await mine
            .CountAsync(
                notification => ArchivedStatuses.Contains(notification.Status), cancellationToken);

        var pinned = await mine.CountAsync(notification => notification.IsPinned, cancellationToken);
        var starred = await mine.CountAsync(notification => notification.IsStarred, cancellationToken);

        var byCategory = await mine
            .GroupBy(notification => notification.CategoryKey)
            .Select(group => new
            {
                CategoryKey = group.Key,
                Unread = group.Count(notification => UnreadStatuses.Contains(notification.Status)),
                Total = group.Count(notification => !ArchivedStatuses.Contains(notification.Status))
            })
            .ToListAsync(cancellationToken);

        return new InboxCountSnapshot
        {
            Unread = unread,
            Total = total,
            Archived = archived,
            Pinned = pinned,
            Starred = starred,
            ByCategory = byCategory.ToDictionary(
                row => row.CategoryKey,
                row => new InboxCategoryCounts { Unread = row.Unread, Total = row.Total },
                StringComparer.Ordinal)
        };
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await Notifications.AddAsync(notification, cancellationToken);
    }

    public void Update(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        NotificationAggregateUpdater.MarkDirty(_dbContext, notification);
    }
}
