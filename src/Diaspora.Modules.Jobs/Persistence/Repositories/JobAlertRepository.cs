using Diaspora.Modules.Jobs.Domain.Alerts;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for standing job alerts.
/// <para>
/// <see cref="GetDueAsync"/> is bounded and tracked, and both matter. Bounded because the sweep
/// runs over every candidate on the platform and an unbounded read would be one query that grows
/// forever; tracked because each alert's watermark advances as its batch completes, so an
/// interrupted pass resumes exactly where it stopped rather than re-sending or skipping.
/// </para>
/// </summary>
public sealed class JobAlertRepository : IJobAlertRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobAlertRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobAlert> Alerts => _dbContext.Set<JobAlert>();

    public async Task<JobAlert?> GetAsync(
        JobAlertId id, CancellationToken cancellationToken = default) =>
        await Alerts.FirstOrDefaultAsync(alert => alert.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobAlert>> GetForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Alerts.AsNoTracking()
            .Where(alert => alert.UserId == userId)
            .OrderByDescending(alert => alert.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Alerts.CountAsync(alert => alert.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<JobAlert>> GetDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default)
    {
        // The interval is the aggregate's own judgement, so the query narrows to active alerts
        // ordered by staleness and IsDue does the rest. Expressing the frequency ladder in SQL
        // would put the rule in two places, and the two would drift.
        return await Alerts
            .Where(alert => alert.IsActive && alert.LastSweptAt <= asOf)
            .OrderBy(alert => alert.LastSweptAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(JobAlert alert, CancellationToken cancellationToken = default) =>
        await Alerts.AddAsync(alert, cancellationToken);

    public void Remove(JobAlert alert) => Alerts.Remove(alert);
}
