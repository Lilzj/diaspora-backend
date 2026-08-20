using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for category attribute definitions.
/// <para>
/// <b><see cref="GetForCategoryPathAsync"/> is the read the whole attribute model turns on.</b> A
/// definition scoped to <c>/electronics/</c> applies to every phone beneath it, so "which fields
/// does this category ask for" is every definition whose path is a <em>prefix</em> of the
/// category's — which is a comparison the database cannot express as an index seek in either
/// direction.
/// </para>
/// <para>
/// It is therefore answered by fetching the handful of candidate ancestors explicitly: a path three
/// levels deep has four possible scopes, and asking for those four by equality is an index seek per
/// level rather than a scan with a computed predicate.
/// </para>
/// </summary>
public sealed class ListingAttributeDefinitionRepository : IListingAttributeDefinitionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ListingAttributeDefinitionRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<ListingAttributeDefinition> Definitions =>
        _dbContext.Set<ListingAttributeDefinition>();

    public async Task<ListingAttributeDefinition?> GetAsync(
        ListingAttributeDefinitionId id, CancellationToken cancellationToken = default) =>
        await Definitions.Include(definition => definition.Choices)
            .FirstOrDefaultAsync(definition => definition.Id == id, cancellationToken);

    public async Task<ListingAttributeDefinition?> GetByKeyAsync(
        string key, string categoryPath, CancellationToken cancellationToken = default)
    {
        var normalizedKey = ListingAttribute.NormalizeKey(key);
        var normalizedPath = ListingAttributeDefinition.NormalizePath(categoryPath);

        if (normalizedKey is null || normalizedPath is null)
        {
            return null;
        }

        return await Definitions.Include(definition => definition.Choices)
            .FirstOrDefaultAsync(
                definition => definition.Key == normalizedKey
                    && definition.CategoryPath == normalizedPath,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingAttributeDefinition>> GetForCategoryPathAsync(
        string categoryPath, CancellationToken cancellationToken = default)
    {
        var normalized = ListingAttributeDefinition.NormalizePath(categoryPath);

        if (normalized is null)
        {
            return [];
        }

        var scopes = AncestorPaths(normalized);

        var rows = await Definitions.AsNoTracking()
            .Include(definition => definition.Choices)
            .Where(definition => scopes.Contains(definition.CategoryPath))
            .ToListAsync(cancellationToken);

        // Deepest scope first, so a definition on the leaf shadows one on an ancestor with the same
        // key. That is what lets "size" mean something specific under clothing while the general
        // one still applies everywhere else.
        return
        [
            .. rows
                .GroupBy(definition => definition.Key, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(
                    definition => definition.CategoryPath.Length).First())
                .OrderBy(definition => definition.Order)
                .ThenBy(definition => definition.Label, StringComparer.Ordinal)
        ];
    }

    public async Task<IReadOnlyList<ListingAttributeDefinition>> GetAllAsync(
        bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = Definitions.AsNoTracking()
            .Include(definition => definition.Choices)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(definition => definition.IsActive);
        }

        return await query
            .OrderBy(definition => definition.CategoryPath)
            .ThenBy(definition => definition.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ListingAttributeDefinition definition, CancellationToken cancellationToken = default) =>
        await Definitions.AddAsync(definition, cancellationToken);

    /// <summary>
    /// Every scope a category path sits under, inclusive and root-first —
    /// <c>/electronics/mobile-phones/</c> yields <c>/</c>, <c>/electronics/</c> and itself.
    /// </summary>
    private static List<string> AncestorPaths(string path)
    {
        var scopes = new List<string> { "/" };
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var accumulated = "/";

        foreach (var segment in segments)
        {
            accumulated += segment + "/";
            scopes.Add(accumulated);
        }

        return scopes;
    }
}
