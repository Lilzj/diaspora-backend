using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Bookmarks.Persistence;

/// <summary>Persistence contract for the <see cref="Bookmark"/> aggregate.</summary>
public interface IBookmarkRepository
{
    Task<IReadOnlyList<Bookmark>> ListByUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<Bookmark?> GetAsync(
        UserId userId, string targetType, string targetId, CancellationToken cancellationToken = default);

    Task<Bookmark?> GetByIdAsync(BookmarkId bookmarkId, CancellationToken cancellationToken = default);

    Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    void Remove(Bookmark bookmark);
}
