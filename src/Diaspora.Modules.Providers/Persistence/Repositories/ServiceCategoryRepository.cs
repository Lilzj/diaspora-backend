using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence.Repositories;

/// <summary>
/// EF Core repository for the service category catalogue.
/// <para>
/// <b><see cref="ResolveAsync"/> is the method that earns the alias table.</b> It matches on
/// slug, then on normalised name, then on any registered alias — which is how a client still
/// sending the frontend's legacy <c>OrgCategory</c> strings (<c>Restaurants</c>,
/// <c>Immigration Consultants</c>) resolves to the right node without an app release. The
/// three-step order matters: slug first because it is unique and indexed, alias last because it
/// is the widest net and the most likely to have a near-miss.
/// </para>
/// </summary>
public sealed class ServiceCategoryRepository : IServiceCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ServiceCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ServiceCategory> Categories => _dbContext.Set<ServiceCategory>();

    private DbSet<Provider> Providers => _dbContext.Set<Provider>();

    public async Task<ServiceCategory?> GetAsync(
        ServiceCategoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Categories)
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<ServiceCategory?> GetBySlugAsync(
        CategorySlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await FullGraph(Categories)
            .FirstOrDefaultAsync(category => category.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceCategory>> GetManyAsync(
        IReadOnlyCollection<ServiceCategoryId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // ── Compared at the value-object level, and that is a correctness fix ────────────────
        //
        // This read was `keys.Contains(category.Id.Value)` over a list of raw Guids. `Id` is a
        // strongly-typed id behind a value converter, so `.Value` is a member access on a type
        // the store does not have: it compiles, it reads perfectly, and EF refuses to translate
        // it at runtime. Every provider search whose page held at least one row therefore
        // answered 500 — the read context resolves each provider's category through here — and
        // nothing caught it because no suite had ever seeded a provider.
        //
        // The converter turns the whole id into its Guid, so comparing whole ids is both correct
        // and the only translatable form. This is the platform's recorded LINQ trap; B4.1 is the
        // second phase to pay for it.
        var keys = ids.ToList();

        return await FullGraph(Categories.AsNoTracking())
            .Where(category => keys.Contains(category.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceCategory>> ListAllAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var query = FullGraph(Categories.AsNoTracking());

        if (!includeRetired)
        {
            query = query.Where(category => category.IsActive);
        }

        // Ordered by path, so the result is already depth-first: a caller building a tree walks
        // it once rather than sorting or recursing. The client's filter row renders straight
        // from this order.
        return await query
            .OrderBy(category => category.Path.Value)
            .ThenBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceCategory>> ListChildrenAsync(
        ServiceCategoryId parentId, CancellationToken cancellationToken = default) =>
        await FullGraph(Categories.AsNoTracking())
            .Where(category => category.ParentId == parentId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ServiceCategory>> ListSubtreeAsync(
        CategoryPath path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        var prefix = path.Value;

        // Tracked, not no-tracking: the only caller is a category move, which then rewrites
        // every descendant's path in the same transaction.
        return await FullGraph(Categories)
            .Where(category => category.Path.Value.StartsWith(prefix))
            .OrderBy(category => category.Path.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceCategory?> ResolveAsync(
        string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var normalized = ServiceCategoryAlias.Normalize(term);

        if (normalized.Length == 0)
        {
            return null;
        }

        var slug = term.Trim().ToLowerInvariant();

        var bySlug = await FullGraph(Categories.AsNoTracking())
            .FirstOrDefaultAsync(category => category.Slug.Value == slug, cancellationToken);

        if (bySlug is not null)
        {
            return bySlug;
        }

        return await FullGraph(Categories.AsNoTracking())
            .FirstOrDefaultAsync(
                category => category.Aliases.Any(alias => alias.Normalized == normalized),
                cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        CategorySlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await Categories.AnyAsync(category => category.Slug.Value == value, cancellationToken);
    }

    public async Task<bool> HasProvidersAsync(
        ServiceCategoryId id, CancellationToken cancellationToken = default) =>
        await Providers.AnyAsync(provider => provider.CategoryId == id, cancellationToken);

    public async Task AddAsync(
        ServiceCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);

    /// <summary>
    /// Aliases, translations and keywords, always.
    /// <para>
    /// The catalogue is a few hundred rows read on nearly every request, and the three owned
    /// collections are what resolution and localisation need. Loading them selectively would
    /// save a handful of rows and produce a second, subtly different read path for whoever
    /// forgets which one they are on.
    /// </para>
    /// </summary>
    private static IQueryable<ServiceCategory> FullGraph(IQueryable<ServiceCategory> query) =>
        query
            .Include(category => category.Aliases)
            .Include(category => category.Translations)
            .Include(category => category.Keywords);
}
