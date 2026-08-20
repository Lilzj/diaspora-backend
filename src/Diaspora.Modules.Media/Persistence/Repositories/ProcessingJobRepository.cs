using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Media.Persistence.Repositories;

/// <summary>EF Core repository for processing jobs.</summary>
public sealed class ProcessingJobRepository : IProcessingJobRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProcessingJobRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<ProcessingJob> Jobs => _dbContext.Set<ProcessingJob>();

    public async Task<ProcessingJob?> GetAsync(
        ProcessingJobId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Jobs
            .Include(job => job.Tasks)
            .FirstOrDefaultAsync(job => job.Id == id, cancellationToken);
    }

    public async Task<ProcessingJob?> GetLatestForMediaAsync(
        MediaId mediaId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        return await Jobs
            .Include(job => job.Tasks)
            .Where(job => job.MediaId == mediaId)
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListForMediaAsync(
        MediaId mediaId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mediaId);

        return await Jobs
            .Include(job => job.Tasks)
            .Where(job => job.MediaId == mediaId)
            .OrderByDescending(job => job.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Oldest first. Newest-first starves under sustained load: the first upload of a busy
    /// hour would be processed after the last, and the member who waited longest waits longest.
    /// </summary>
    public async Task<IReadOnlyList<ProcessingJob>> FindPendingAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Jobs
            .Include(job => job.Tasks)
            .Where(job => job.Status == ProcessingJobStatus.Pending)
            .OrderBy(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProcessingJob>> FindRetryableAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Jobs
            .Include(job => job.Tasks)
            .Where(job =>
                job.Status == ProcessingJobStatus.Failed &&
                job.Attempts < ProcessingJob.MaxAttempts)
            .OrderBy(job => job.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default) =>
        await Jobs.AddAsync(job, cancellationToken);
}
