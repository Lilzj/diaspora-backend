using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Bookmarks.Persistence;

/// <summary>
/// EF Core repository over the shared <c>DiasporaDbContext</c>.
/// <para>
/// A module reaching the shared context directly is the modular-monolith trade working as
/// intended: one connection, one transaction, one save path — and the module still owns its
/// own aggregate, its own query shapes and its own tables. Reads are untracked; only the
/// command paths that mutate load tracked.
/// </para>
/// </summary>
public sealed class BookmarkRepository : IBookmarkRepository
{
    private readonly DiasporaDbContext _dbContext;

    public BookmarkRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<Bookmark> Bookmarks => _dbContext.Set<Bookmark>();

    public async Task<IReadOnlyList<Bookmark>> ListByUserAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        await Bookmarks
            .AsNoTracking()
            .Where(bookmark => bookmark.UserId == userId)
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Bookmark?> GetAsync(
        UserId userId, string targetType, string targetId, CancellationToken cancellationToken = default) =>
        await Bookmarks.FirstOrDefaultAsync(
            bookmark => bookmark.UserId == userId &&
                        bookmark.TargetType == targetType &&
                        bookmark.TargetId == targetId,
            cancellationToken);

    public async Task<Bookmark?> GetByIdAsync(
        BookmarkId bookmarkId, CancellationToken cancellationToken = default) =>
        await Bookmarks.FirstOrDefaultAsync(bookmark => bookmark.Id == bookmarkId, cancellationToken);

    public async Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default) =>
        await Bookmarks.AddAsync(bookmark, cancellationToken);

    public void Remove(Bookmark bookmark) => Bookmarks.Remove(bookmark);
}
