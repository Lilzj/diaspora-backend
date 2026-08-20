using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence.Repositories;

/// <summary>
/// EF Core repository for provider reviews.
/// <para>
/// <see cref="GetRatingDistributionAsync"/> is the single source of truth behind every rating on
/// the platform. The provider's stored summary is a cache of this query's result, recomputed
/// after every change rather than incremented — see <c>ProviderReviewSummary</c> for why the
/// incremented alternative drifts silently.
/// </para>
/// </summary>
public sealed class ProviderReviewRepository : IProviderReviewRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProviderReviewRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ProviderReview> Reviews => _dbContext.Set<ProviderReview>();

    public async Task<ProviderReview?> GetAsync(
        ProviderReviewId id, CancellationToken cancellationToken = default) =>
        await Reviews.FirstOrDefaultAsync(review => review.Id == id, cancellationToken);

    public async Task<ProviderReview?> FindByReviewerAsync(
        ProviderId providerId, UserId reviewerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(reviewerId);

        return await Reviews.FirstOrDefaultAsync(
            review => review.ProviderId == providerId && review.ReviewerId == reviewerId,
            cancellationToken);
    }

    public async Task<CursorPagedResult<ProviderReview>> ListForProviderAsync(
        ProviderId providerId,
        string? cursor,
        int limit,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        var query = Reviews.AsNoTracking().Where(review => review.ProviderId == providerId);

        if (!includeHidden)
        {
            query = query.Where(review => review.Status == ReviewStatus.Published);
        }

        var parsed = ProviderCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            // Not `CreatedAt < after` alone, which drops every review sharing the boundary
            // instant. Reviews tie after an import, and the ORDER BY's secondary key — the
            // rating — is nowhere near unique, so ties here are wide. See CursorBoundary.
            var tie = CursorBoundary.Exclusion(parsed.LastSlug, ProviderReviewId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(review =>
                review.CreatedAt < after
                || (review.CreatedAt == after && !exhausted && !seen.Contains(review.Id)));
        }

        var pageSize = Math.Clamp(limit, 1, 100);

        var items = await query
            .OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Rating)
            .ThenByDescending(review => review.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        return Page(items, pageSize, parsed);
    }

    public async Task<CursorPagedResult<ProviderReview>> ListByReviewerAsync(
        UserId reviewerId, string? cursor, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        var query = Reviews.AsNoTracking().Where(review => review.ReviewerId == reviewerId);

        var parsed = ProviderCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastSlug, ProviderReviewId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(review =>
                review.CreatedAt < after
                || (review.CreatedAt == after && !exhausted && !seen.Contains(review.Id)));
        }

        var pageSize = Math.Clamp(limit, 1, 100);

        var items = await query
            .OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Rating)
            .ThenByDescending(review => review.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        return Page(items, pageSize, parsed);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(
        ProviderId providerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        var rows = await Reviews
            .AsNoTracking()
            .Where(review =>
                review.ProviderId == providerId && review.Status == ReviewStatus.Published)
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Rating, row => row.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, int>>>
        GetRatingDistributionsAsync(
            IReadOnlyCollection<ProviderId> providerIds,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyDictionary<int, int>>();
        }

        var keys = providerIds.ToList();

        var rows = await Reviews
            .AsNoTracking()
            .Where(review =>
                keys.Contains(review.ProviderId)
                && review.Status == ReviewStatus.Published)
            .GroupBy(review => new { review.ProviderId, review.Rating })
            .Select(group => new
            {
                group.Key.ProviderId,
                group.Key.Rating,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ProviderId.Value)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<int, int>)group.ToDictionary(
                    row => row.Rating, row => row.Count));
    }

    public async Task AddAsync(
        ProviderReview review, CancellationToken cancellationToken = default) =>
        await Reviews.AddAsync(review, cancellationToken);

    public void Remove(ProviderReview review) => Reviews.Remove(review);

    private static CursorPagedResult<ProviderReview> Page(
        List<ProviderReview> items, int pageSize, ProviderCursor? previous)
    {
        var hasMore = items.Count > pageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore && items.Count > 0
            ? ProviderCursor.FromTimestamp(
                items[^1].CreatedAt,
                CursorBoundary.Key(
                    items,
                    review => review.CreatedAt,
                    review => review.Id.Value,
                    previous?.LastSlug,
                    previous?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<ProviderReview>.Create(items, nextCursor, hasMore);
    }
}
