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

    Task<int> CountOpenAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task AddAsync(ContentReport report, CancellationToken cancellationToken = default);
}
