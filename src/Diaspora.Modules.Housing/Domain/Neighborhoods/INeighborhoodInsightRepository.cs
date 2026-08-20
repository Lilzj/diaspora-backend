using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>How the module reaches its community insights and their endorsements.</summary>
public interface INeighborhoodInsightRepository
{
    Task<NeighborhoodInsight?> GetAsync(
        NeighborhoodInsightId id, CancellationToken cancellationToken = default);

    Task<NeighborhoodInsight?> GetForReadAsync(
        NeighborhoodInsightId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<NeighborhoodInsight>> QueryAsync(
        NeighborhoodInsightQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Published insights for one place. What the profile counter is set from.</summary>
    Task<int> CountApprovedForAreaAsync(
        Guid areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this member has already said this about this place.
    /// <para>
    /// Compared on the fingerprint rather than the raw text, so the same observation resubmitted
    /// with different spacing is caught - which is what an impatient member does when the first
    /// submission does not appear, because it is waiting for a moderator.
    /// </para>
    /// </summary>
    Task<bool> HasDuplicateAsync(
        Guid areaId,
        UserId authorId,
        string fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>How many insights this member has submitted since a moment. The abuse bound.</summary>
    Task<int> CountByAuthorSinceAsync(
        UserId authorId, DateTimeOffset since, CancellationToken cancellationToken = default);

    Task AddAsync(NeighborhoodInsight insight, CancellationToken cancellationToken = default);

    // ── Endorsements ────────────────────────────────────────────────────────────────────────

    Task<bool> HasEndorsedAsync(
        NeighborhoodInsightId insightId,
        UserId memberId,
        CancellationToken cancellationToken = default);

    Task<int> CountEndorsementsAsync(
        NeighborhoodInsightId insightId, CancellationToken cancellationToken = default);

    Task AddEndorsementAsync(
        NeighborhoodInsightEndorsement endorsement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws an endorsement. Hard-deleted - see the entity for why a retracted opinion is not
    /// kept as a tombstone.
    /// </summary>
    Task RemoveEndorsementAsync(
        NeighborhoodInsightId insightId,
        UserId memberId,
        CancellationToken cancellationToken = default);
}
