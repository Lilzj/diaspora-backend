using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Sets;

/// <summary>
/// The generated-set store.
/// <para>
/// <b><see cref="FindFreshAsync"/> is the read that decides whether the engine runs at all.</b> A
/// member who pulls to refresh three times in ten seconds should get the page they already have,
/// not three ranking passes — and the alternative to a persisted set is a cache, which would answer
/// "why am I seeing this" with whatever the cache happened to still hold.
/// </para>
/// </summary>
public interface IRecommendationSetRepository
{
    void Add(RecommendationSet set);

    Task<RecommendationSet?> FindAsync(
        RecommendationSetId setId, CancellationToken cancellationToken = default);

    /// <summary>The member's current page for a surface, if it has not expired. See the class summary.</summary>
    Task<RecommendationSet?> FindFreshAsync(
        UserId userId,
        RecommendationSurface surface,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The most recent set that contained a target, whether or not it is fresh.
    /// <para>
    /// How feedback recovers a strategy and a reason when the client reports an interaction without
    /// a set identifier — an older app version, a card restored from a saved screen.
    /// </para>
    /// </summary>
    Task<RecommendationSet?> FindMostRecentContainingAsync(
        UserId userId,
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationSet>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Sets generated in a window, for the analytics rollup.</summary>
    Task<IReadOnlyList<RecommendationSet>> ListGeneratedBetweenAsync(
        DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken cancellationToken = default);

    void Update(RecommendationSet set);

    /// <summary>Retention. Hard-deletes sets past the window — see <c>RecommendationOptions</c>.</summary>
    Task<int> DeleteOlderThanAsync(
        DateTimeOffset generatedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Erasure.</summary>
    Task<int> DeleteForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
