using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for comments.</summary>
public sealed class CommentRepository : ICommentRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommentRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Comment> Comments => _dbContext.Set<Comment>();

    public async Task<Comment?> GetAsync(
        CommentId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Comments
            .Include(comment => comment.Mentions)
            .FirstOrDefaultAsync(comment => comment.Id == id, cancellationToken);
    }

    public async Task<CursorPagedResult<Comment>> ListRootsAsync(
        ContentReference content,
        FeedCursor? cursor,
        int limit,
        bool includeHidden,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var query = Comments
            .Include(comment => comment.Mentions)
            .Where(comment =>
                comment.Content.Type == content.Type &&
                comment.Content.Id == content.Id &&
                comment.ParentCommentId == null);

        if (!includeHidden)
        {
            query = query.Where(comment => comment.Status != CommentStatus.Hidden);
        }

        if (cursor is not null)
        {
            var (start, end) = FeedBoundary.Window(cursor);
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, CommentId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            // Two faults were live here. The boundary dropped every comment sharing the last
            // one's millisecond — and a burst of replies to the same post shares one readily.
            // And the ordering leads with IsPinned while the cursor compared only CreatedAt, so
            // a pinned comment, being older than the boundary, satisfied the filter and sorted
            // straight back to the top of every page. The cursor has to carry every column the
            // ORDER BY leads with. See CursorBoundary.
            query = cursor.LeadingFlag
                // Still inside the pinned block: continue through it, then everything unpinned,
                // all of which sorts after any pinned comment however old.
                ? query.Where(comment =>
                    !comment.IsPinned
                    || comment.CreatedAt < start
                    || (comment.CreatedAt >= start
                        && comment.CreatedAt < end
                        && !exhausted && !seen.Contains(comment.Id)))
                : query.Where(comment =>
                    !comment.IsPinned
                    && (comment.CreatedAt < start
                        || (comment.CreatedAt >= start
                            && comment.CreatedAt < end
                            && !exhausted && !seen.Contains(comment.Id))));
        }

        var items = await query
            .OrderByDescending(comment => comment.IsPinned)
            .ThenByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].CreatedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items, comment => comment.CreatedAt, comment => comment.Id.Value, cursor),
                items[^1].IsPinned).Encode()
            : null;

        return CursorPagedResult<Comment>.Create(items, next, hasMore);
    }

    /// <summary>
    /// A subtree, by prefix scan on the materialised path. The reason the path column exists.
    /// </summary>
    public async Task<IReadOnlyList<Comment>> ListSubtreeAsync(
        Comment root, int maxDepth, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        var prefix = root.Path + Comment.PathSeparator;

        return await Comments
            .Include(comment => comment.Mentions)
            .Where(comment =>
                comment.Path.StartsWith(prefix) && comment.Depth <= root.Depth + maxDepth)
            .OrderBy(comment => comment.Path)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comment>> ListRepliesAsync(
        IReadOnlyCollection<CommentId> rootIds,
        int maxPerRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootIds);

        if (rootIds.Count == 0)
        {
            return [];
        }

        // One query for every root's replies, rather than one per root. The per-root cap is
        // applied after materialising, because a per-group limit is not translatable on every
        // provider and correctness here matters more than shaving a round trip.
        var replies = await Comments
            .Include(comment => comment.Mentions)
            .Where(comment =>
                comment.ParentCommentId != null && rootIds.Contains(comment.ParentCommentId))
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);

        return replies
            .GroupBy(comment => comment.ParentCommentId!)
            .SelectMany(group => group.Take(maxPerRoot))
            .ToList();
    }

    public async Task<int> CountAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Comments.CountAsync(
            comment => comment.Content.Type == content.Type && comment.Content.Id == content.Id,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountManyAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        var ids = contents.Select(content => content.Id).Distinct().ToList();

        var rows = await Comments
            .Where(comment => ids.Contains(comment.Content.Id))
            .GroupBy(comment => new { comment.Content.Type, comment.Content.Id })
            .Select(group => new { group.Key.Type, group.Key.Id, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => ContentReference.Create(row.Type, row.Id).Key,
            row => row.Count,
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<Comment>> ListByAuthorAsync(
        UserId authorId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return await Comments
            .Where(comment => comment.AuthorId == authorId)
            .OrderByDescending(comment => comment.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Comment comment, CancellationToken cancellationToken = default) =>
        await Comments.AddAsync(comment, cancellationToken);

    public void Remove(Comment comment) => Comments.Remove(comment);
}
