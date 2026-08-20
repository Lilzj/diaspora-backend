using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for the category tree.
/// <para>
/// Read whole rather than paged: it is a few hundred rows that change when code ships. A caching
/// decorator can sit in front of this without any caller changing, which is the reason the
/// interface says nothing about how it is fetched.
/// </para>
/// </summary>
public sealed class ListingCategoryRepository : IListingCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ListingCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingCategory> Categories => _dbContext.Set<ListingCategory>();

    public async Task<ListingCategory?> GetAsync(
        ListingCategoryId id, CancellationToken cancellationToken = default) =>
        await Categories.FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<ListingCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = ListingCategory.NormalizeKey(key);

        return normalized is null
            ? null
            : await Categories.FirstOrDefaultAsync(
                category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ListingCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Select(ListingCategory.NormalizeKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Dictionary<string, ListingCategory>(StringComparer.Ordinal);
        }

        var rows = await Categories.AsNoTracking()
            .Where(category => normalized.Contains(category.Key))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(category => category.Key, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<ListingCategory>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = Categories.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(category => category.IsActive);
        }

        // Ordered by path, then by the node's own order. Path first is what makes the flat list
        // renderable as a tree without a second sort on the client.
        return await query
            .OrderBy(category => category.Depth)
            .ThenBy(category => category.Order)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ListingCategory>> GetSubtreeAsync(
        string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        var prefix = path;

        return await Categories.AsNoTracking()
            .Where(category => category.Path.StartsWith(prefix))
            .OrderBy(category => category.Depth)
            .ThenBy(category => category.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasChildrenAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = ListingCategory.NormalizeKey(key);

        return normalized is not null
            && await Categories.AnyAsync(
                category => category.ParentKey == normalized, cancellationToken);
    }

    public async Task AddAsync(
        ListingCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);
}
