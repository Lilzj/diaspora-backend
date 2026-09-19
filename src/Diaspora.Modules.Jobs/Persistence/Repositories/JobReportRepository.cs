using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>EF Core repository for reports about job adverts.</summary>
public sealed class JobReportRepository : IJobReportRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobReport> Reports => _dbContext.Set<JobReport>();

    public async Task<JobReport?> GetAsync(
        JobReportId id, CancellationToken cancellationToken = default) =>
        await Reports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobReport>> ListAsync(
        bool openOnly,
        bool fraudFirst,
        JobPostingId? postingId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Reports.AsQueryable();

        if (openOnly)
        {
            query = query.Where(report =>
                report.Status == JobReportStatus.Open
                || report.Status == JobReportStatus.UnderReview);
        }

        if (postingId is not null)
        {
            query = query.Where(report => report.PostingId == postingId);
        }

        // Ordered so the reports about money reach a moderator first. Translated rather than
        // sorted in memory: the queue is read a page at a time and a client-side sort would
        // order only the page it happened to fetch.
        query = fraudFirst
            ? query
                .OrderByDescending(report =>
                    report.Reason == JobReportReason.Scam
                    || report.Reason == JobReportReason.FeeRequested)
                .ThenBy(report => report.CreatedAt)
            : query.OrderBy(report => report.CreatedAt);

        return await query.Take(limit).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobReport>> GetOpenForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Reports
            .Where(report =>
                report.PostingId == postingId
                && (report.Status == JobReportStatus.Open
                    || report.Status == JobReportStatus.UnderReview))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasReportedAsync(
        JobPostingId postingId, UserId reportedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(reportedBy);

        return await Reports.AnyAsync(
            report => report.PostingId == postingId && report.ReportedBy == reportedBy,
            cancellationToken);
    }

    public async Task<int> CountForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Reports.CountAsync(
            report => report.PostingId == postingId, cancellationToken);
    }

    public async Task<int> CountUpheldForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        return await Reports.CountAsync(
            report => report.EmployerId == employerId
                && report.Status == JobReportStatus.Upheld,
            cancellationToken);
    }

    public async Task AddAsync(JobReport report, CancellationToken cancellationToken = default) =>
        await Reports.AddAsync(report, cancellationToken);
}
