using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the Knowledge Directory's navigation catalogue.
/// <para>
/// <b>Every read loads the whole graph, and that is affordable because the catalogue is small by
/// design.</b> Twenty-odd rows, each with a handful of translations and sources. The alternative —
/// a lean read for listings and a full one for editing — would save a join on a query that runs
/// once per country page and is cached, and would introduce the class of bug where a caller
/// operates on a category whose sources silently did not load.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and
/// owned collections only when asked — the trap that makes a category look correct until somebody
/// reads its sources and finds none.
/// </para>
/// </summary>
public sealed class KnowledgeCategoryRepository : IKnowledgeCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public KnowledgeCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<KnowledgeCategory> Categories => _dbContext.Set<KnowledgeCategory>();

    public async Task<KnowledgeCategory?> GetAsync(
        KnowledgeCategoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Categories)
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<KnowledgeCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = KnowledgeNormalization.Key(key);

        return normalized is null
            ? null
            : await FullGraph(Categories)
                .FirstOrDefaultAsync(category => category.Key == normalized, cancellationToken);
    }

    /// <summary>
    /// The one read that sees past the global query filter.
    /// <para>
    /// Archiving is a soft delete, so the filter hides the row — and a restore has to load exactly
    /// the row the archiving hid. Without this, restore could only ever answer "not found", which
    /// is the one answer that is certainly wrong for a category the curator can see in their own
    /// archive list. The platform has recorded this blind spot once already; this is the fix
    /// applied before it is paid for again.
    /// </para>
    /// </summary>
    public async Task<KnowledgeCategory?> GetIncludingArchivedAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = KnowledgeNormalization.Key(key);

        return normalized is null
            ? null
            : await FullGraph(Categories.IgnoreQueryFilters())
                .FirstOrDefaultAsync(category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeCategory>> ListAsync(
        bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = includeArchived
            ? FullGraph(Categories.IgnoreQueryFilters().AsNoTracking())
            : FullGraph(Categories.AsNoTracking());

        return await query
            .OrderBy(category => category.Order)
            .ThenBy(category => category.Key)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Several categories by key, in one round trip.
    /// <para>
    /// The N+1 guard every composed read in this phase depends on: a country directory of twenty
    /// entries resolves its labels here once rather than twenty times, and the dictionary it returns
    /// is what the composer indexes into.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, KnowledgeCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Select(KnowledgeNormalization.Key)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Dictionary<string, KnowledgeCategory>(StringComparer.Ordinal);
        }

        var found = await FullGraph(Categories.AsNoTracking())
            .Where(category => normalized.Contains(category.Key))
            .ToListAsync(cancellationToken);

        return found.ToDictionary(category => category.Key, StringComparer.Ordinal);
    }

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = KnowledgeNormalization.Key(key);

        // IgnoreQueryFilters, because an archived category still holds its key: the unique index
        // would refuse the insert anyway, and reusing the key would make restoring the archived one
        // impossible.
        return normalized is not null
            && await Categories
                .IgnoreQueryFilters()
                .AnyAsync(category => category.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(
        KnowledgeCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);

    /// <summary>
    /// Archives, through the platform's soft-delete path.
    /// <para>
    /// The handler never stamps the archival itself. It marks the aggregate for removal and saves;
    /// the shared context converts that into a soft delete, sets the timestamp and the actor, and
    /// leaves the row where every audit and every restore can find it. Stamping it by hand is the
    /// defect B1.0.1 records, and it is silent — a hand-stamped delete looks correct in the response
    /// either way.
    /// </para>
    /// </summary>
    public void Remove(KnowledgeCategory category) => Categories.Remove(category);

    private static IQueryable<KnowledgeCategory> FullGraph(IQueryable<KnowledgeCategory> query) =>
        query
            .Include(category => category.Localizations)
            .Include(category => category.Sources)
            .Include(category => category.ProviderCategories);
}
