using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Pipelines;

/// <summary>Reads and writes hiring pipelines.</summary>
public interface IHiringPipelineRepository
{
    Task<HiringPipeline?> GetAsync(
        HiringPipelineId id, CancellationToken cancellationToken = default);

    /// <summary>The board for a posting, tracked. How every stage move validates its target.</summary>
    Task<HiringPipeline?> GetForPostingAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    /// <summary>The board for a posting, untracked. For the kanban read.</summary>
    Task<HiringPipeline?> GetForPostingForReadAsync(
        JobPostingId postingId, CancellationToken cancellationToken = default);

    Task AddAsync(HiringPipeline pipeline, CancellationToken cancellationToken = default);

    void Remove(HiringPipeline pipeline);
}
