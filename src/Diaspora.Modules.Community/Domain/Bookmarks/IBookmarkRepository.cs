using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Bookmarks;

/// <summary>Reads and writes bookmarks and the folders they sit in.</summary>
public interface IBookmarkRepository
{
    Task<CommunityBookmark?> FindAsync(
        UserId userId, ContentReference content, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<CommunityBookmark>> ListAsync(
        UserId userId,
        CommunityContentType? contentType,
        BookmarkFolderId? folderId,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts per content type, which is exactly the shape the client's saved screen renders as
    /// its collection tiles. One grouped query rather than one query per tile.
    /// </summary>
    Task<IReadOnlyDictionary<CommunityContentType, int>> CountByTypeAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ContentReference content, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountManyAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    /// <summary>Which of these did the viewer save. Batched for the same reason as everything else.</summary>
    Task<IReadOnlySet<string>> FindSavedAsync(
        UserId userId,
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BookmarkFolder>> ListFoldersAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<BookmarkFolder?> GetFolderAsync(
        BookmarkFolderId folderId, CancellationToken cancellationToken = default);

    Task AddAsync(CommunityBookmark bookmark, CancellationToken cancellationToken = default);

    Task AddFolderAsync(BookmarkFolder folder, CancellationToken cancellationToken = default);

    void Remove(CommunityBookmark bookmark);

    void RemoveFolder(BookmarkFolder folder);
}
