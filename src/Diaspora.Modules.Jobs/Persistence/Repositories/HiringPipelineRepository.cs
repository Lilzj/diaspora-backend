using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Pipelines;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for hiring pipelines.
/// <para>
/// The stages are always included: a pipeline without them is an empty board, and every reason
/// to load one — validating a stage move, rendering the kanban, editing the columns — needs
/// them.
/// </para>
/// </summary>
public sealed class HiringPipelineRepository : IHiringPipelineRepository
{
    private readonly DiasporaDbContext _dbContext;

    public HiringPipelineRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HiringPipeline> Pipelines => _dbContext.Set<HiringPipeline>();

    public async Task<HiringPipeline?> GetAsync(
        HiringPipelineId id, CancellationToken cancellationToken = default) =>
        await Pipelines
            .Include(pipeline => pipeline.Stages)
            .FirstOrDefaultAsync(pipeline => pipeline.Id == id, cancellationToken);

    public async Task<HiringPipeline?> GetForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Pipelines
            .Include(pipeline => pipeline.Stages)
            .FirstOrDefaultAsync(pipeline => pipeline.PostingId == postingId, cancellationToken);
    }

    public async Task<HiringPipeline?> GetForPostingForReadAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postingId);

        return await Pipelines.AsNoTracking()
            .Include(pipeline => pipeline.Stages)
            .FirstOrDefaultAsync(pipeline => pipeline.PostingId == postingId, cancellationToken);
    }

    public async Task AddAsync(
        HiringPipeline pipeline, CancellationToken cancellationToken = default) =>
        await Pipelines.AddAsync(pipeline, cancellationToken);

    public void Remove(HiringPipeline pipeline) => Pipelines.Remove(pipeline);
}
