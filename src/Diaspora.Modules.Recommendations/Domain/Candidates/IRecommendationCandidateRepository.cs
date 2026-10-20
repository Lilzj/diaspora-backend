using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Candidates;

/// <summary>
/// The candidate projection store.
/// <para>
/// <b><see cref="GetOrProjectAsync"/> is an upsert, and it must be, because integration event
/// delivery is at-least-once and unordered.</b> The same <c>ListingPublished</c> event can arrive
/// twice, and a <c>ListingUpdated</c> can arrive before the <c>ListingPublished</c> it updates. A
/// projection that assumed creation-then-update would either duplicate rows or drop the update
/// entirely, and both failures are invisible until somebody notices a vertical is under-recommended.
/// </para>
/// <para>
/// The change-tracker check inside the implementation is the same correctness point B13 records for
/// the inbox: one event can produce two projections for one target, and a second query would not see
/// the first.
/// </para>
/// </summary>
public interface IRecommendationCandidateRepository
{
    Task<RecommendationCandidate?> FindAsync(
        RecommendationTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>The projection, created on first sight. See the class summary for why it is an upsert.</summary>
    Task<RecommendationCandidate> GetOrProjectAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        string sourceModule,
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>The engine's pool. Coarse, bounded, and not personalised — see <see cref="CandidateQuery"/>.</summary>
    Task<IReadOnlyList<RecommendationCandidate>> SearchAsync(
        CandidateQuery query, CancellationToken cancellationToken = default);

    /// <summary>Specific candidates by reference. Used to render a stored set whose items are ids.</summary>
    Task<IReadOnlyList<RecommendationCandidate>> ListByTargetsAsync(
        IReadOnlyCollection<(RecommendationTargetType TargetType, Guid TargetId)> targets,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most engaged-with candidates, for a country or globally.
    /// <para>
    /// The cold-start pool and the trending surface both read this. Kept as its own method rather
    /// than a <see cref="CandidateQuery"/> flag because it orders by popularity rather than filtering
    /// on it, and a query object that could change the ordering would be a ranking implementation.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<RecommendationCandidate>> ListMostPopularAsync(
        IReadOnlyList<RecommendationTargetType> targetTypes,
        string? countryCode,
        int limit,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The live candidates a member owns, of the given types — the monetization surface's "what is
    /// yours to promote". Withdrawn, deleted and expired rows are excluded; newest first, bounded.
    /// </summary>
    Task<IReadOnlyList<RecommendationCandidate>> ListOwnedLiveAsync(
        UserId ownerUserId,
        IReadOnlyCollection<RecommendationTargetType> targetTypes,
        DateTimeOffset asOf,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Candidates a member owns. The self-authored policy's input.</summary>
    Task<IReadOnlyList<Guid>> ListOwnedTargetIdsAsync(
        UserId ownerUserId,
        RecommendationTargetType targetType,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(
        RecommendationTargetType targetType, CancellationToken cancellationToken = default);

    void Update(RecommendationCandidate candidate);

    /// <summary>
    /// Retention. Retires withdrawn candidates whose window has closed.
    /// <para>
    /// The candidate is soft-deletable, so this removes it from every query rather than from the
    /// table — which is what keeps "was recommendable until Tuesday" answerable for the analytics
    /// long after the source unpublished it.
    /// </para>
    /// </summary>
    Task<int> DeleteWithdrawnAsync(
        DateTimeOffset withdrawnBefore, int batchSize, CancellationToken cancellationToken = default);
}
