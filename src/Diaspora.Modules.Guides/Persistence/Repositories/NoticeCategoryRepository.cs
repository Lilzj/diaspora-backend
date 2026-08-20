using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for the notice category catalogue.
/// <para>
/// Every read loads the whole graph, which is affordable because the catalogue is small by design —
/// twenty-odd rows with a handful of translations each. B8.1's catalogue repository documents the
/// trade in full and it is unchanged here.
/// </para>
/// <para>
/// <b>Owned collections must be explicitly included.</b> EF loads owned references eagerly and owned
/// collections only when asked — the trap that makes a category look correct until somebody reads its
/// translations and finds none.
/// </para>
/// </summary>
public sealed class NoticeCategoryRepository : INoticeCategoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NoticeCategoryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NoticeCategory> Categories => _dbContext.Set<NoticeCategory>();

    public async Task<NoticeCategory?> GetAsync(
        NoticeCategoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Categories)
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken);

    public async Task<NoticeCategory?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = IntelligenceNormalization.Key(key);

        return normalized is null
            ? null
            : await FullGraph(Categories)
                .FirstOrDefaultAsync(category => category.Key == normalized, cancellationToken);
    }

    /// <summary>
    /// The one read that sees past the global query filter.
    /// <para>
    /// Archiving is a soft delete, so the filter hides the row — and a restore has to load exactly the
    /// row the archiving hid. The platform has recorded this blind spot once already; this is the fix
    /// applied before it is paid for a third time.
    /// </para>
    /// </summary>
    public async Task<NoticeCategory?> GetIncludingArchivedAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = IntelligenceNormalization.Key(key);

        return normalized is null
            ? null
            : await FullGraph(Categories.IgnoreQueryFilters())
                .FirstOrDefaultAsync(category => category.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<NoticeCategory>> ListAsync(
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

    public async Task<IReadOnlyDictionary<string, NoticeCategory>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Select(IntelligenceNormalization.Key)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return new Dictionary<string, NoticeCategory>(StringComparer.Ordinal);
        }

        var found = await FullGraph(Categories.AsNoTracking())
            .Where(category => normalized.Contains(category.Key))
            .ToListAsync(cancellationToken);

        return found.ToDictionary(category => category.Key, StringComparer.Ordinal);
    }

    public async Task<bool> KeyExistsAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = IntelligenceNormalization.Key(key);

        // IgnoreQueryFilters, because an archived category still holds its key: the unique index
        // would refuse the insert anyway, and reusing the key would make restoring the archived one
        // impossible.
        return normalized is not null
            && await Categories
                .IgnoreQueryFilters()
                .AnyAsync(category => category.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(
        NoticeCategory category, CancellationToken cancellationToken = default) =>
        await Categories.AddAsync(category, cancellationToken);

    /// <inheritdoc cref="Domain.Intelligence.IOfficialNoticeRepository.Remove" />
    public void Remove(NoticeCategory category) => Categories.Remove(category);

    private static IQueryable<NoticeCategory> FullGraph(IQueryable<NoticeCategory> query) =>
        query.Include(category => category.Localizations);
}
