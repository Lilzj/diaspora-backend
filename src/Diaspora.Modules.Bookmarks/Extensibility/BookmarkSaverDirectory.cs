using Diaspora.Contracts.Bookmarks.V1.Savers;
using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Bookmarks.Extensibility;

/// <summary>
/// Answers <see cref="ISaverDirectory"/> from this module's own rows — 21 Oct 2026.
/// <para>
/// <b>The identifier is matched in the spellings a client sends.</b> <c>TargetId</c> is a free string
/// stored as given (trimmed), and a Guid reaches it lower-case from a JSON client and upper-case from
/// anything that formats one by hand. Both <c>D</c>-format spellings are asked for, so a saver is not
/// missed for the case of their identifier.
/// </para>
/// <para>
/// <b>Bounded.</b> The limit is clamped to <see cref="MaximumLimit"/> whatever the caller asks, and the
/// most recent savers are the ones kept.
/// </para>
/// </summary>
public sealed class BookmarkSaverDirectory : ISaverDirectory
{
    /// <summary>The most savers one call returns.</summary>
    public const int MaximumLimit = 1_000;

    private readonly DiasporaDbContext _dbContext;

    public BookmarkSaverDirectory(DiasporaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<Guid>> ListSaversAsync(
        string targetType, Guid targetId, int limit, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetType) || targetId == Guid.Empty || limit <= 0)
        {
            return [];
        }

        var type = targetType.Trim().ToLowerInvariant();
        var lower = targetId.ToString("D");
        var upper = lower.ToUpperInvariant();

        var savers = await _dbContext.Set<Bookmark>()
            .AsNoTracking()
            .Where(bookmark => bookmark.TargetType == type
                && (bookmark.TargetId == lower || bookmark.TargetId == upper))
            .OrderByDescending(bookmark => bookmark.CreatedAt)
            .Select(bookmark => bookmark.UserId)
            .Take(Math.Min(limit, MaximumLimit))
            .ToListAsync(cancellationToken);

        return [.. savers.Select(userId => userId.Value).Distinct()];
    }
}
