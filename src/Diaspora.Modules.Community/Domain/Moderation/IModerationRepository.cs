using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Moderation;

/// <summary>Reads and writes moderation decisions and automated reviews.</summary>
public interface IModerationRepository
{
    /// <summary>The decision history for one piece of content, newest first. Append-only.</summary>
    Task<IReadOnlyList<ModerationDecision>> ListDecisionsAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<ModerationDecision>> ListRecentDecisionsAsync(
        FeedCursor? cursor, int limit, CancellationToken cancellationToken = default);

    Task<ContentReview?> GetLatestReviewAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ContentReview>> GetLatestReviewsAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    Task AddDecisionAsync(
        ModerationDecision decision, CancellationToken cancellationToken = default);

    Task AddReviewAsync(ContentReview review, CancellationToken cancellationToken = default);
}
