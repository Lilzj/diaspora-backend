using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the guide catalogue. Small by design — a few dozen rows read on
/// nearly every write path — so the listing is never paged and the key lookup is the hot path.
/// </summary>
public sealed class GuideCategoryRepository : IGuideCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public GuideCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<GuideCategory> Categories => _dbContext.Set<GuideCategory>();

    public async Task<GuideCategory?> GetAsync(
        GuideCategoryId id, CancellationToken cancellationToken = default) =>
        await Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<GuideCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return await Categories.FirstOrDefaultAsync(
            category => category.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<GuideCategory>> ListAsync(
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

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return await Categories.AnyAsync(category => category.Key == key, cancellationToken);
    }

    public async Task AddAsync(
        GuideCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);
}
