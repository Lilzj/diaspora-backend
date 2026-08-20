using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>EF Core repository for the watchlist.</summary>
public sealed class SavedListingRepository : ISavedListingRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SavedListingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SavedListing> Saved => _dbContext.Set<SavedListing>();

    public async Task<SavedListing?> GetAsync(
        SavedListingId id, CancellationToken cancellationToken = default) =>
        await Saved.FirstOrDefaultAsync(saved => saved.Id == id, cancellationToken);

    public async Task<SavedListing?> FindAsync(
        UserId userId,
        MarketplaceListingId listingId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(listingId);

        return await Saved.FirstOrDefaultAsync(
            saved => saved.UserId == userId && saved.ListingId == listingId, cancellationToken);
    }

    /// <summary>
    /// Which of these listings the viewer has saved.
    /// <para>
    /// One query for a whole page of cards. Returns raw <c>Guid</c>s because the caller checks
    /// membership rather than identity, and because a set of strongly-typed identifiers would make
    /// every card's lookup allocate.
    /// </para>
    /// </summary>
    public async Task<IReadOnlySet<Guid>> FindSavedListingIdsAsync(
        UserId userId,
        IReadOnlyCollection<MarketplaceListingId> listingIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(listingIds);

        if (listingIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var keys = listingIds.Distinct().ToList();

        var rows = await Saved.AsNoTracking()
            .Where(saved => saved.UserId == userId && keys.Contains(saved.ListingId))
            .Select(saved => saved.ListingId)
            .ToListAsync(cancellationToken);

        return rows.Select(id => id.Value).ToHashSet();
    }

    public async Task<CursorPagedResult<SavedListing>> QueryForUserAsync(
        UserId userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var parsed = MarketplaceCursor.Parse(cursor);

        var query = ApplyCursor(
            Saved.AsNoTracking().Where(saved => saved.UserId == userId), parsed);

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? MarketplaceCursor.FromTimestamp(
                rows[^1].CreatedAt,
                CursorBoundary.Key(
                    rows,
                    saved => saved.CreatedAt,
                    saved => saved.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<SavedListing>.Create(rows, next, hasMore);
    }

    public async Task<int> CountForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Saved.AsNoTracking()
            .CountAsync(saved => saved.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<SavedListing>> GetPriceWatchersAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Saved.AsNoTracking()
            .Where(saved => saved.ListingId == listingId && saved.NotifyOnPriceChange)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(SavedListing saved, CancellationToken cancellationToken = default) =>
        await Saved.AddAsync(saved, cancellationToken);

    public void Remove(SavedListing saved) => Saved.Remove(saved);

    /// <summary>
    /// Most recently saved first, tie-broken by identifier through <see cref="CursorBoundary"/>.
    /// <para>
    /// The degenerate case is ordinary here: somebody browsing a category saves five listings in a
    /// few seconds, and <c>CreatedAt</c> is stamped by the shared persistence interceptor, so a
    /// strict comparison would drop all but the first of them from the watchlist.
    /// </para>
    /// </summary>
    private static IQueryable<SavedListing> ApplyCursor(
        IQueryable<SavedListing> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            var tie = CursorBoundary.Exclusion(cursor.LastKey, SavedListingId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(saved =>
                saved.CreatedAt < value
                || (saved.CreatedAt == value && !exhausted && !seen.Contains(saved.Id)));
        }

        return query
            .OrderByDescending(saved => saved.CreatedAt)
            .ThenBy(saved => saved.Id);
    }
}
