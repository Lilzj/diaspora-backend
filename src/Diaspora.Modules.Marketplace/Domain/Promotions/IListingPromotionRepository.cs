using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Domain.Promotions;

/// <summary>How the application layer reaches promotions.</summary>
public interface IListingPromotionRepository
{
    Task<ListingPromotion?> GetAsync(
        ListingPromotionId id, CancellationToken cancellationToken = default);

    /// <summary>Every promotion on a listing, live or finished.</summary>
    Task<IReadOnlyList<ListingPromotion>> GetForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing's live promotions. What decides whether ending one should take the featured
    /// flag down — a listing with two overlapping promotions keeps the flag until the last ends.
    /// </summary>
    Task<IReadOnlyList<ListingPromotion>> GetActiveForListingAsync(
        MarketplaceListingId listingId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>The sweep's work list: promotions whose window has closed but not been concluded.</summary>
    Task<IReadOnlyList<ListingPromotion>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(ListingPromotion promotion, CancellationToken cancellationToken = default);
}
