using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for the employment-domain catalogue.
/// <para>
/// The catalogue is small — a few dozen rows that change once a release — so
/// <see cref="GetAllAsync"/> reads it whole rather than paging it, and a caching decorator can
/// sit in front without any of this changing.
/// </para>
/// </summary>
public sealed class JobCategoryRepository : IJobCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobCategory> Categories => _dbContext.Set<JobCategory>();

    public async Task<JobCategory?> GetAsync(
        JobCategoryId id, CancellationToken cancellationToken = default) =>
        await Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<JobCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = JobCategory.NormalizeKey(key);

        return normalized is null
            ? null
            : await Categories.FirstOrDefaultAsync(
                category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<JobCategory>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = Categories.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        return await query
            .OrderBy(category => category.Order)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Select(JobCategory.NormalizeKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        // Tracked: the seeder upserts in one pass, so it needs to mutate what it reads.
        return await Categories
            .Where(category => normalized.Contains(category.Key))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = JobCategory.NormalizeKey(key);

        return normalized is not null
            && await Categories.AnyAsync(
                category => category.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(
        JobCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);
}
