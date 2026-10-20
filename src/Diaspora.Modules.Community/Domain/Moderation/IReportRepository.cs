using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Moderation;

/// <summary>Reads and writes reports.</summary>
public interface IReportRepository
{
    Task<ContentReport?> GetAsync(
        ContentReportId id, CancellationToken cancellationToken = default);

    Task<ContentReport?> FindAsync(
        UserId reporterId, ContentReference content, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContentReport>> ListForContentAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    /// <summary>The moderator queue: open reports, grouped by the content they are about.</summary>
    Task<CursorPagedResult<ContentReport>> ListOpenAsync(
        CommunityContentType? contentType,
        ReportReasonCode? reason,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reports about content published under one publisher's name — a post published as the
    /// publisher, or a comment under such a post — oldest first. An empty
    /// <paramref name="statuses"/> means every status. Added 21 Oct 2026 for the per-community
    /// moderation queue.
    /// </summary>
    Task<CursorPagedResult<ContentReport>> ListAboutPublisherAsync(
        Guid publisherId,
        IReadOnlyCollection<ReportStatus> statuses,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the content is a post published as the publisher, or a comment under one.</summary>
    Task<bool> IsAboutPublisherAsync(
        ContentReference content, Guid publisherId, CancellationToken cancellationToken = default);

    Task<int> CountOpenAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task AddAsync(ContentReport report, CancellationToken cancellationToken = default);
}
