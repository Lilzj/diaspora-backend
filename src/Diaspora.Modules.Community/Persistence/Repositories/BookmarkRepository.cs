using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for bookmarks and folders.</summary>
public sealed class BookmarkRepository : IBookmarkRepository
{
    private readonly DiasporaDbContext _dbContext;

    public BookmarkRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityBookmark> Bookmarks => _dbContext.Set<CommunityBookmark>();

    private DbSet<BookmarkFolder> Folders => _dbContext.Set<BookmarkFolder>();

    public async Task<CommunityBookmark?> FindAsync(
        UserId userId, ContentReference content, CancellationToken cancellationToken = default)
    {
        var key = CommunityBookmark.BuildKey(userId, content);

        return await Bookmarks.FirstOrDefaultAsync(
            bookmark => bookmark.ContentKey == key, cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityBookmark>> ListAsync(
        UserId userId,
        CommunityContentType? contentType,
        BookmarkFolderId? folderId,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var query = Bookmarks.Where(bookmark => bookmark.UserId == userId);

        if (contentType is { } type)
        {
            query = query.Where(bookmark => bookmark.Content.Type == type);
        }

        if (folderId is not null)
        {
            query = query.Where(bookmark => bookmark.FolderId == folderId);
        }

        if (cursor is not null)
        {
            // Rows before the boundary millisecond, plus whatever of that millisecond the
            // previous page had not already returned. A plain `SavedAt < after` drops the whole
            // tie group, and bookmarks tie constantly — somebody working through a results page
            // taps five in a second. See CursorBoundary.
            var (start, end) = FeedBoundary.Window(cursor);
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, CommunityBookmarkId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(bookmark =>
                bookmark.SavedAt < start
                || (bookmark.SavedAt >= start
                    && bookmark.SavedAt < end
                    && !exhausted && !seen.Contains(bookmark.Id)));
        }

        var items = await query
            .OrderByDescending(bookmark => bookmark.SavedAt)
            .ThenByDescending(bookmark => bookmark.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].SavedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items, bookmark => bookmark.SavedAt, bookmark => bookmark.Id.Value, cursor))
                .Encode()
            : null;

        return CursorPagedResult<CommunityBookmark>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyDictionary<CommunityContentType, int>> CountByTypeAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var rows = await Bookmarks
            .Where(bookmark => bookmark.UserId == userId)
            .GroupBy(bookmark => bookmark.Content.Type)
            .Select(group => new { Type = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Type, row => row.Count);
    }

    public async Task<int> CountAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Bookmarks.CountAsync(
            bookmark =>
                bookmark.Content.Type == content.Type && bookmark.Content.Id == content.Id,
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

        var rows = await Bookmarks
            .Where(bookmark => ids.Contains(bookmark.Content.Id))
            .GroupBy(bookmark => new { bookmark.Content.Type, bookmark.Content.Id })
            .Select(group => new { group.Key.Type, group.Key.Id, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            row => ContentReference.Create(row.Type, row.Id).Key,
            row => row.Count,
            StringComparer.Ordinal);
    }

    public async Task<IReadOnlySet<string>> FindSavedAsync(
        UserId userId,
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var keys = contents
            .Select(content => CommunityBookmark.BuildKey(userId, content))
            .ToList();

        var found = await Bookmarks
            .Where(bookmark => keys.Contains(bookmark.ContentKey))
            .Select(bookmark => new { bookmark.Content.Type, bookmark.Content.Id })
            .ToListAsync(cancellationToken);

        return found
            .Select(row => ContentReference.Create(row.Type, row.Id).Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<BookmarkFolder>> ListFoldersAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Folders
            .Where(folder => folder.OwnerId == userId)
            .OrderBy(folder => folder.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<BookmarkFolder?> GetFolderAsync(
        BookmarkFolderId folderId, CancellationToken cancellationToken = default) =>
        await Folders.FirstOrDefaultAsync(folder => folder.Id == folderId, cancellationToken);

    public async Task AddAsync(
        CommunityBookmark bookmark, CancellationToken cancellationToken = default) =>
        await Bookmarks.AddAsync(bookmark, cancellationToken);

    public async Task AddFolderAsync(
        BookmarkFolder folder, CancellationToken cancellationToken = default) =>
        await Folders.AddAsync(folder, cancellationToken);

    public void Remove(CommunityBookmark bookmark) => Bookmarks.Remove(bookmark);

    public void RemoveFolder(BookmarkFolder folder) => Folders.Remove(folder);
}
