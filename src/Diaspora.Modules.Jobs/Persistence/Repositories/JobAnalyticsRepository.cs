using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>EF Core repository for per-posting analytics rollups.</summary>
public sealed class JobAnalyticsRepository : IJobAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobAnalyticsRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobAnalytics> Analytics => _dbContext.Set<JobAnalytics>();

    public async Task<JobAnalytics?> GetForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Analytics.FirstOrDefaultAsync(
            analytics => analytics.PostingId == postingId, cancellationToken);
    }

    public async Task<JobAnalytics?> GetForPostingForReadAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Analytics.AsNoTracking().FirstOrDefaultAsync(
            analytics => analytics.PostingId == postingId, cancellationToken);
    }

    public async Task<IReadOnlyList<JobAnalytics>> GetManyAsync(
        IReadOnlyCollection<JobPostingId> postingIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingIds);

        if (postingIds.Count == 0)
        {
            return [];
        }

        // Whole identifiers: `.Value` has no column of its own, so EF cannot translate it.
        var keys = postingIds.ToList();

        // One query for a dashboard listing twenty postings with their funnels.
        return await Analytics.AsNoTracking()
            .Where(analytics => keys.Contains(analytics.PostingId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobAnalytics>> GetForEmployerAsync(
        EmployerProfileId employerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        return await Analytics.AsNoTracking()
            .Where(analytics => analytics.EmployerId == employerId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        JobAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);
}
