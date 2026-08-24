using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Reviews;

/// <summary>Persistence contract for <see cref="SellerReview"/>.</summary>
public interface ISellerReviewRepository
{
    Task<SellerReview?> GetAsync(SellerReviewId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The review already written for this purchase, if any.
    /// <para>
    /// Keyed on the reservation rather than the seller, which is what lets a repeat buyer rate
    /// each purchase separately while still writing only one review per purchase.
    /// </para>
    /// </summary>
    Task<SellerReview?> FindByReservationAsync(
        ListingReservationId reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The five bucket counts for one seller, over published reviews only.
    /// <para>
    /// A <c>GROUP BY rating</c>, which is what makes the rollup reconstructible rather than
    /// incremented. Buckets absent from the result are zero.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(
        SellerProfileId sellerId, CancellationToken cancellationToken = default);

    /// <summary>The seller's published reviews, newest first.</summary>
    Task<CursorPagedResult<SellerReview>> ListForSellerAsync(
        SellerProfileId sellerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Everything one member has written, including what they later withdrew.</summary>
    Task<CursorPagedResult<SellerReview>> ListByReviewerAsync(
        UserId reviewerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(SellerReview review, CancellationToken cancellationToken = default);
}
