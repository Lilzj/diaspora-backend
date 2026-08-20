using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for reports. Every path is behind the moderation permission.
/// <para>
/// The queue is ordered with fraud and impersonation first. Both are claims that somebody is being
/// deceived right now — a fake charity collection takes money from people who cannot afford to lose
/// it — and an hour in the queue matters for those in a way it does not for a duplicate listing.
/// </para>
/// </summary>
public sealed class CommunityLifeReportRepository : ICommunityLifeReportRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityLifeReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityLifeReport> Reports => _dbContext.Set<CommunityLifeReport>();

    public async Task<CommunityLifeReport?> GetAsync(
        CommunityLifeReportId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Reports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public async Task<bool> HasReportedAsync(
        ReportSubjectType subjectType,
        Guid subjectId,
        UserId reportedByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reportedByUserId);

        return await Reports
            .AsNoTracking()
            .AnyAsync(
                report => report.SubjectType == subjectType
                    && report.SubjectId == subjectId
                    && report.ReportedByUserId == reportedByUserId,
                cancellationToken);
    }

    public async Task<int> CountForSubjectAsync(
        ReportSubjectType subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default) =>
        await Reports
            .AsNoTracking()
            .CountAsync(
                report => report.SubjectType == subjectType && report.SubjectId == subjectId,
                cancellationToken);

    public async Task<CursorPagedResult<CommunityLifeReport>> SearchAsync(
        ReportSubjectType? subjectType,
        IReadOnlyList<CommunityLifeReportStatus>? statuses,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Reports.AsNoTracking();

        if (subjectType is { } subject)
        {
            query = query.Where(report => report.SubjectType == subject);
        }

        if (statuses is { Count: > 0 })
        {
            var wanted = statuses.ToList();
            query = query.Where(report => wanted.Contains(report.Status));
        }

        var parsed = CommunityLifeCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastKey, CommunityLifeReportId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(report =>
                report.CreatedAt < boundary
                || (report.CreatedAt == boundary && !exhausted && !seen.Contains(report.Id)));
        }

        // Deception first. See the class summary.
        query = query
            .OrderByDescending(report =>
                report.Reason == CommunityLifeReportReason.Fraud
                || report.Reason == CommunityLifeReportReason.Impersonation)
            .ThenByDescending(report => report.CreatedAt)
            .ThenBy(report => report.Id);

        var effectiveLimit = Math.Clamp(limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityLifeCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<CommunityLifeReport>.Create(rows, next, hasMore);
    }

    public async Task AddAsync(
        CommunityLifeReport report, CancellationToken cancellationToken = default) =>
        await Reports.AddAsync(report, cancellationToken);

    public void Update(CommunityLifeReport report) => Reports.Update(report);
}
