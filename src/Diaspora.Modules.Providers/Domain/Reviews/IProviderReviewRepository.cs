using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Providers.Domain.Reviews;

/// <summary>
/// Reads and writes provider reviews.
/// <para>
/// <see cref="GetRatingDistributionAsync"/> is the method that keeps ratings honest. The
/// provider's <c>ProviderReviewSummary</c> is denormalised, and the only way it can be trusted
/// is if it is <em>recomputed</em> from this query rather than incremented on write — a
/// summary maintained by <c>+1</c> drifts the first time a review is edited from four stars to
/// two, hidden by a moderator, or withdrawn, and it drifts silently.
/// </para>
/// </summary>
public interface IProviderReviewRepository
{
    Task<ProviderReview?> GetAsync(
        ProviderReviewId id, CancellationToken cancellationToken = default);

    /// <summary>A member's existing review of a provider, if any. Enforces one review each.</summary>
    Task<ProviderReview?> FindByReviewerAsync(
        ProviderId providerId, UserId reviewerId, CancellationToken cancellationToken = default);

    /// <summary>A page of a provider's reviews, newest first.</summary>
    Task<CursorPagedResult<ProviderReview>> ListForProviderAsync(
        ProviderId providerId,
        string? cursor,
        int limit,
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    /// <summary>Reviews a member has written. The client's "My Reviews" screen.</summary>
    Task<CursorPagedResult<ProviderReview>> ListByReviewerAsync(
        UserId reviewerId, string? cursor, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// The five bucket counts for a provider, counting only reviews that qualify.
    /// The single source of truth behind every rating on the platform.
    /// </summary>
    Task<IReadOnlyDictionary<int, int>> GetRatingDistributionAsync(
        ProviderId providerId, CancellationToken cancellationToken = default);

    /// <summary>Distributions for several providers at once, for a rebuild sweep.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, int>>> GetRatingDistributionsAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default);

    Task AddAsync(ProviderReview review, CancellationToken cancellationToken = default);

    void Remove(ProviderReview review);
}
