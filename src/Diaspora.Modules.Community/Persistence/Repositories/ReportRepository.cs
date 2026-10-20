using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for reports.</summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ContentReport> Reports => _dbContext.Set<ContentReport>();

    public async Task<ContentReport?> GetAsync(
        ContentReportId id, CancellationToken cancellationToken = default) =>
        await Reports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);

    public async Task<ContentReport?> FindAsync(
        UserId reporterId, ContentReference content, CancellationToken cancellationToken = default)
    {
        var key = ContentReport.BuildKey(reporterId, content);

        return await Reports.FirstOrDefaultAsync(
            report => report.ReporterKey == key, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentReport>> ListForContentAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Reports
            .Where(report =>
                report.Content.Type == content.Type && report.Content.Id == content.Id)
            .OrderBy(report => report.ReportedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<ContentReport>> ListOpenAsync(
        CommunityContentType? contentType,
        ReportReasonCode? reason,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Reports.Where(report =>
            report.Status == ReportStatus.Open || report.Status == ReportStatus.Reviewed);

        if (contentType is { } type)
        {
            query = query.Where(report => report.Content.Type == type);
        }

        if (reason is { } code)
        {
            query = query.Where(report => report.Reason == code);
        }

        return await PageAsync(query, cursor, limit, cancellationToken);
    }

    public async Task<CursorPagedResult<ContentReport>> ListAboutPublisherAsync(
        Guid publisherId,
        IReadOnlyCollection<ReportStatus> statuses,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        // Two reads for the identifiers, then the reports. The post identifier is a converted
        // strongly-typed key and the report's content identifier a plain Guid, so a join would have
        // to go through EF.Property — which compiles and then fails on the first cursor here (see
        // the cursor-paging note). The lists are one community's posts and their comments.
        var postIds = await PostIdsPublishedAsAsync(publisherId, cancellationToken);

        if (postIds.Count == 0)
        {
            return CursorPagedResult<ContentReport>.Create([], null, false);
        }

        var commentIds = await CommentIdsUnderAsync(postIds, cancellationToken);

        var query = Reports.Where(report =>
            (report.Content.Type == CommunityContentType.Post && postIds.Contains(report.Content.Id))
            || (report.Content.Type == CommunityContentType.Comment
                && commentIds.Contains(report.Content.Id)));

        if (statuses.Count > 0)
        {
            var wanted = statuses.Distinct().ToList();
            query = query.Where(report => wanted.Contains(report.Status));
        }

        return await PageAsync(query, cursor, limit, cancellationToken);
    }

    public async Task<bool> IsAboutPublisherAsync(
        ContentReference content, Guid publisherId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var postId = content.Type switch
        {
            CommunityContentType.Post => content.Id,
            CommunityContentType.Comment => await PostUnderCommentAsync(content.Id, cancellationToken),
            _ => (Guid?)null
        };

        if (postId is not { } id)
        {
            return false;
        }

        var typed = CommunityPostId.From(id);

        return await _dbContext.Set<CommunityPost>().AnyAsync(
            post => post.Id == typed && post.Publisher.OrganizationId == publisherId,
            cancellationToken);
    }

    private async Task<List<Guid>> PostIdsPublishedAsAsync(
        Guid publisherId, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Set<CommunityPost>()
            .Where(post => post.Publisher.OrganizationId == publisherId)
            .Select(post => post.Id)
            .ToListAsync(cancellationToken);

        return [.. ids.Select(id => id.Value)];
    }

    private async Task<List<Guid>> CommentIdsUnderAsync(
        List<Guid> postIds, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Set<Comment>()
            .Where(comment => comment.Content.Type == CommunityContentType.Post
                && postIds.Contains(comment.Content.Id))
            .Select(comment => comment.Id)
            .ToListAsync(cancellationToken);

        return [.. ids.Select(id => id.Value)];
    }

    private async Task<Guid?> PostUnderCommentAsync(Guid commentId, CancellationToken cancellationToken)
    {
        var typed = CommentId.From(commentId);

        var target = await _dbContext.Set<Comment>()
            .Where(comment => comment.Id == typed)
            .Select(comment => new { comment.Content.Type, comment.Content.Id })
            .FirstOrDefaultAsync(cancellationToken);

        return target is { Type: CommunityContentType.Post } ? target.Id : null;
    }

    private static async Task<CursorPagedResult<ContentReport>> PageAsync(
        IQueryable<ContentReport> query,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (cursor is not null)
        {
            // Oldest-first, so the window is mirrored: everything after the boundary
            // millisecond, plus the part of it this page has not already shown. A brigading
            // incident produces dozens of reports on one instant, and a plain
            // `ReportedAt > after` would drop every one but the first — which is exactly the
            // queue a moderator most needs to see whole. See CursorBoundary.
            var (start, end) = FeedBoundary.Window(cursor);
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, ContentReportId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(report =>
                report.ReportedAt >= end
                || (report.ReportedAt >= start
                    && report.ReportedAt < end
                    && !exhausted && !seen.Contains(report.Id)));
        }

        // Oldest first. A queue that showed the newest report first would leave the oldest
        // complaint unanswered for as long as new ones kept arriving.
        var items = await query
            .OrderBy(report => report.ReportedAt)
            .ThenBy(report => report.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].ReportedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items, report => report.ReportedAt, report => report.Id.Value, cursor))
                .Encode()
            : null;

        return CursorPagedResult<ContentReport>.Create(items, next, hasMore);
    }

    public async Task<int> CountOpenAsync(
        ContentReference content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return await Reports.CountAsync(
            report =>
                report.Content.Type == content.Type &&
                report.Content.Id == content.Id &&
                (report.Status == ReportStatus.Open || report.Status == ReportStatus.Reviewed),
            cancellationToken);
    }

    public async Task AddAsync(
        ContentReport report, CancellationToken cancellationToken = default) =>
        await Reports.AddAsync(report, cancellationToken);
}
