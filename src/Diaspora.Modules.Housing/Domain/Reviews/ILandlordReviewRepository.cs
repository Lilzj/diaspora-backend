using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Reviews;

/// <summary>Persistence contract for <see cref="LandlordReview"/>.</summary>
public interface ILandlordReviewRepository
{
    Task<LandlordReview?> GetAsync(
        LandlordReviewId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The review already written for this tenancy, if any.
    /// <para>
    /// Keyed on the reservation rather than the landlord, which is what lets a returning tenant
    /// rate each tenancy separately while still writing only one review per tenancy.
    /// </para>
    /// </summary>
    Task<LandlordReview?> FindByReservationAsync(
        HousingReservationId reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The five bucket counts for one landlord, over published reviews only.
    /// <para>
    /// A <c>GROUP BY rating</c>, which is what makes the rollup reconstructible rather than
    /// incremented. Buckets absent from the result are zero.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default);

    /// <summary>The landlord's published reviews, newest first.</summary>
    Task<CursorPagedResult<LandlordReview>> ListForLandlordAsync(
        LandlordProfileId landlordId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Everything one member has written, including what they later withdrew.</summary>
    Task<CursorPagedResult<LandlordReview>> ListByReviewerAsync(
        UserId reviewerId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(LandlordReview review, CancellationToken cancellationToken = default);
}
