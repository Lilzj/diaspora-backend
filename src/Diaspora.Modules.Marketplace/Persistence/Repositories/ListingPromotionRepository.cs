using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>EF Core repository for promotions.</summary>
public sealed class ListingPromotionRepository : IListingPromotionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ListingPromotionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingPromotion> Promotions => _dbContext.Set<ListingPromotion>();

    public async Task<ListingPromotion?> GetAsync(
        ListingPromotionId id, CancellationToken cancellationToken = default) =>
        await Promotions.FirstOrDefaultAsync(promotion => promotion.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ListingPromotion>> GetForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Promotions.AsNoTracking()
            .Where(promotion => promotion.ListingId == listingId)
            .OrderByDescending(promotion => promotion.StartsAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The listing's live promotions.
    /// <para>
    /// What decides whether ending one should take the featured flag down: a listing with two
    /// overlapping promotions keeps the flag until the last of them ends, and a handler that did
    /// not ask would strip it on the first.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ListingPromotion>> GetActiveForListingAsync(
        MarketplaceListingId listingId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Promotions.AsNoTracking()
            .Where(promotion =>
                promotion.ListingId == listingId
                && !promotion.IsCancelled
                && promotion.StartsAt <= asOf
                && promotion.EndsAt > asOf)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ListingPromotion>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Promotions
            .Where(promotion => !promotion.IsCancelled && promotion.EndsAt <= asOf)
            .OrderBy(promotion => promotion.EndsAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        ListingPromotion promotion, CancellationToken cancellationToken = default) =>
        await Promotions.AddAsync(promotion, cancellationToken);
}
