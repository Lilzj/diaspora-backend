using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Offers;

/// <summary>How the application layer reaches negotiations.</summary>
public interface IListingOfferRepository
{
    /// <summary>The full graph including the history, tracked. For a command.</summary>
    Task<ListingOffer?> GetAsync(
        ListingOfferId id, CancellationToken cancellationToken = default);

    Task<ListingOffer?> GetForReadAsync(
        ListingOfferId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The buyer's open negotiation on a listing, if there is one. What enforces
    /// "one open negotiation per buyer per listing" before a second is created.
    /// </summary>
    Task<ListingOffer?> GetOpenForBuyerAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<ListingOffer>> QueryAsync(
        OfferQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(OfferQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many offers a member has made since an instant. The abuse bound.</summary>
    Task<int> CountByBuyerSinceAsync(
        UserId buyerUserId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>The expiry sweep's work list, oldest first.</summary>
    Task<IReadOnlyList<ListingOffer>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every open negotiation on a listing. What a sale settles: an item that has sold cannot
    /// leave four buyers waiting on offers nobody will ever answer.
    /// </summary>
    Task<IReadOnlyList<ListingOffer>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    Task AddAsync(ListingOffer offer, CancellationToken cancellationToken = default);
}
