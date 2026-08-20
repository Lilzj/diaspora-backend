using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Feedback;

/// <summary>
/// The feedback store.
/// <para>
/// <b><see cref="ListSuppressedTargetIdsAsync"/> is on the hot path and returns identifiers rather
/// than aggregates on purpose.</b> Every generation asks "what has this member already refused", and
/// the answer feeds a <c>HashSet</c>; materialising the aggregates would load a member's whole
/// dismissal history to build a set of GUIDs and throw the rest away.
/// </para>
/// </summary>
public interface IRecommendationFeedbackRepository
{
    void Add(RecommendationFeedback feedback);

    /// <summary>
    /// Targets a member's dismissals still suppress, for one type.
    /// <para>
    /// The dismissal policy's whole input. Returns raw identifiers — see the class summary.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListSuppressedTargetIdsAsync(
        UserId userId,
        RecommendationTargetType targetType,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>Every suppressed target across every type. The home feed's case.</summary>
    Task<IReadOnlyList<(RecommendationTargetType TargetType, Guid TargetId)>> ListAllSuppressedAsync(
        UserId userId, DateTimeOffset asOf, CancellationToken cancellationToken = default);

    /// <summary>Whether the member already dismissed this target inside the window.</summary>
    Task<bool> IsSuppressedAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's most recent feedback of a kind on a target.
    /// <para>
    /// Used to make repeated reports idempotent: a client that retries a view report should not
    /// inflate the denominator of every rate on the surface.
    /// </para>
    /// </summary>
    Task<RecommendationFeedback?> FindLatestAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        RecommendationFeedbackKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>Feedback the analytics sweep has not folded into a rollup, oldest first.</summary>
    Task<IReadOnlyList<RecommendationFeedback>> ListUnrolledAsync(
        int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationFeedback>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    void Update(RecommendationFeedback feedback);

    /// <summary>Retention.</summary>
    Task<int> DeleteOlderThanAsync(
        DateTimeOffset occurredBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Erasure.</summary>
    Task<int> DeleteForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
