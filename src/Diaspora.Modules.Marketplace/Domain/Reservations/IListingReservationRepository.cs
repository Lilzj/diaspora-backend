using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Reservations;

/// <summary>How the application layer reaches reservations.</summary>
public interface IListingReservationRepository
{
    Task<ListingReservation?> GetAsync(
        ListingReservationId id, CancellationToken cancellationToken = default);

    Task<ListingReservation?> GetForReadAsync(
        ListingReservationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The buyer's live reservation on a listing, if there is one. What stops a buyer holding the
    /// same item twice and taking two units off the market for one purchase.
    /// </summary>
    Task<ListingReservation?> GetActiveForBuyerAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<ListingReservation>> QueryAsync(
        ReservationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ReservationQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many live reservations a member holds across the whole marketplace.</summary>
    Task<int> CountActiveByBuyerAsync(
        UserId buyerUserId, CancellationToken cancellationToken = default);

    /// <summary>The expiry sweep's work list, oldest window first.</summary>
    Task<IReadOnlyList<ListingReservation>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>Every live reservation on a listing. What a seller's dashboard shows.</summary>
    Task<IReadOnlyList<ListingReservation>> GetActiveForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    Task AddAsync(ListingReservation reservation, CancellationToken cancellationToken = default);
}
