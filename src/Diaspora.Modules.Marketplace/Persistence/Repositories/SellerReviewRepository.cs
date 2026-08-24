using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>Reads and writes <see cref="SellerReview"/>.</summary>
public sealed class SellerReviewRepository : ISellerReviewRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SellerReviewRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SellerReview> Reviews => _dbContext.Set<SellerReview>();

    public async Task<SellerReview?> GetAsync(
        SellerReviewId id, CancellationToken cancellationToken = default) =>
        await Reviews.FirstOrDefaultAsync(review => review.Id == id, cancellationToken);

    public async Task<SellerReview?> FindByReservationAsync(
        ListingReservationId reservationId, CancellationToken cancellationToken = default) =>
        Reviews.Local.FirstOrDefault(review => review.ReservationId == reservationId)
        ?? await Reviews.FirstOrDefaultAsync(
            review => review.ReservationId == reservationId, cancellationToken);

    /// <summary>
    /// The <c>GROUP BY rating</c> the rollup is rebuilt from.
    /// <para>
    /// Published only. A hidden or withdrawn review must stop counting the moment its status
    /// changes, and filtering here is what makes that true without a second code path.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(
        SellerProfileId sellerId, CancellationToken cancellationToken = default)
    {
        var buckets = await Reviews
            .AsNoTracking()
            .Where(review => review.SellerId == sellerId
                && review.Status == SellerReviewStatus.Published)
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return buckets.ToDictionary(bucket => bucket.Rating, bucket => bucket.Count);
    }

    public async Task<CursorPagedResult<SellerReview>> ListForSellerAsync(
        SellerProfileId sellerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        await PageAsync(
            Reviews.AsNoTracking()
                .Where(review => review.SellerId == sellerId
                    && review.Status == SellerReviewStatus.Published),
            cursor,
            limit,
            cancellationToken);

    /// <summary>
    /// Everything one member wrote, whatever its status.
    /// <para>
    /// Unfiltered on purpose: this is the member's own list, and a review they withdrew
    /// vanishing from it would look like the platform lost it.
    /// </para>
    /// </summary>
    public async Task<CursorPagedResult<SellerReview>> ListByReviewerAsync(
        UserId reviewerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        await PageAsync(
            Reviews.AsNoTracking().Where(review => review.ReviewerId == reviewerId),
            cursor,
            limit,
            cancellationToken);

    public async Task AddAsync(
        SellerReview review, CancellationToken cancellationToken = default) =>
        await Reviews.AddAsync(review, cancellationToken);

    private static async Task<CursorPagedResult<SellerReview>> PageAsync(
        IQueryable<SellerReview> source,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var parsed = MarketplaceCursor.Parse(cursor);

        var query = source.OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id);

        var filtered = parsed?.AsTimestamp() is { } after
            ? query.Where(review => review.CreatedAt < after)
            : query;

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);
        var rows = await filtered.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

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
                    review => review.CreatedAt,
                    review => review.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<SellerReview>.Create(rows, next, hasMore);
    }
}
