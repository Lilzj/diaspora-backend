using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>Reads and writes <see cref="LandlordReview"/>.</summary>
public sealed class LandlordReviewRepository : ILandlordReviewRepository
{
    private readonly DiasporaDbContext _dbContext;

    public LandlordReviewRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<LandlordReview> Reviews => _dbContext.Set<LandlordReview>();

    public async Task<LandlordReview?> GetAsync(
        LandlordReviewId id, CancellationToken cancellationToken = default) =>
        await Reviews.FirstOrDefaultAsync(review => review.Id == id, cancellationToken);

    /// <summary>
    /// Local first, then the database.
    /// <para>
    /// A query cannot see an insert that is still in the change tracker, and a caller that
    /// writes a review and then asks for it back inside one unit of work would be told there is
    /// none — which is how B21's confirmation handler came to insert two reputation profiles
    /// and violate a unique index.
    /// </para>
    /// </summary>
    public async Task<LandlordReview?> FindByReservationAsync(
        HousingReservationId reservationId, CancellationToken cancellationToken = default) =>
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
        LandlordProfileId landlordId, CancellationToken cancellationToken = default)
    {
        var buckets = await Reviews
            .AsNoTracking()
            .Where(review => review.LandlordId == landlordId
                && review.Status == LandlordReviewStatus.Published)
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return buckets.ToDictionary(bucket => bucket.Rating, bucket => bucket.Count);
    }

    public async Task<CursorPagedResult<LandlordReview>> ListForLandlordAsync(
        LandlordProfileId landlordId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default) =>
        await PageAsync(
            Reviews.AsNoTracking()
                .Where(review => review.LandlordId == landlordId
                    && review.Status == LandlordReviewStatus.Published),
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
    public async Task<CursorPagedResult<LandlordReview>> ListByReviewerAsync(
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
        LandlordReview review, CancellationToken cancellationToken = default) =>
        await Reviews.AddAsync(review, cancellationToken);

    private static async Task<CursorPagedResult<LandlordReview>> PageAsync(
        IQueryable<LandlordReview> source,
        string? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        var parsed = HousingCursor.Parse(cursor);
        var query = source;

        if (parsed?.AsTimestamp() is { } boundary)
        {
            // Strictly before the boundary instant, plus whatever remains of the tie group at
            // it. A plain `< boundary` drops every row sharing the instant, and the instant is
            // shared constantly: the persistence interceptor stamps CreatedAt, so everything
            // written in one unit of work ties.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, LandlordReviewId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(review =>
                review.CreatedAt < boundary
                || (review.CreatedAt == boundary && !exhausted && !seen.Contains(review.Id)));
        }

        query = query
            .OrderByDescending(review => review.CreatedAt)
            .ThenBy(review => review.Id);

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? HousingCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<LandlordReview>.Create(rows, next, hasMore);
    }
}
