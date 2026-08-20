using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Catalog;

/// <summary>Reads and writes the employment-domain catalogue.</summary>
public interface IJobCategoryRepository
{
    Task<JobCategory?> GetAsync(JobCategoryId id, CancellationToken cancellationToken = default);

    /// <summary>One category by its stable key, tracked. What a posting's category is validated against.</summary>
    Task<JobCategory?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue, untracked. Small enough to read whole and cache.</summary>
    Task<IReadOnlyList<JobCategory>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>Several by key, tracked. The seeder's read, so it can upsert in one pass.</summary>
    Task<IReadOnlyList<JobCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(JobCategory category, CancellationToken cancellationToken = default);
}
