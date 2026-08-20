using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for country knowledge directories.
/// <para>
/// <b>Two graph shapes, and the split is the difference between one screen and a hundred and
/// ninety-five.</b> The full graph loads entries, the featured shelf and translations, because the
/// country page renders all three. The header graph loads none of them, because the country picker
/// renders a name and a count — and a grid that eagerly loaded every country's arrangement would be
/// several thousand rows to draw a list of names.
/// </para>
/// </summary>
public sealed class CountryKnowledgeDirectoryRepository : ICountryKnowledgeDirectoryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryKnowledgeDirectoryRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<CountryKnowledgeDirectory> Directories =>
        _dbContext.Set<CountryKnowledgeDirectory>();

    public async Task<CountryKnowledgeDirectory?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await FullGraph(Directories)
                .FirstOrDefaultAsync(
                    directory => directory.CountryCode == country, cancellationToken);
    }

    public async Task<CountryKnowledgeDirectory?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await FullGraph(Directories.AsNoTracking())
                .FirstOrDefaultAsync(
                    directory => directory.CountryCode == country, cancellationToken);
    }

    public async Task<CountryKnowledgeDirectory?> GetByIdAsync(
        CountryKnowledgeDirectoryId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Directories)
            .FirstOrDefaultAsync(directory => directory.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<CountryKnowledgeDirectory?> GetIncludingArchivedAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await FullGraph(Directories.IgnoreQueryFilters())
                .FirstOrDefaultAsync(
                    directory => directory.CountryCode == country, cancellationToken);
    }

    public async Task<IReadOnlyList<CountryKnowledgeDirectory>> ListHeadersAsync(
        bool publishedOnly = true, CancellationToken cancellationToken = default)
    {
        // No Include at all. The country grid renders names and counts, and the counts come from
        // the analytics and context projections rather than from the arrangement.
        var query = Directories.AsNoTracking();

        if (publishedOnly)
        {
            query = query.Where(directory => directory.IsPublished);
        }

        return await query
            .OrderBy(directory => directory.DisplayName)
            .ThenBy(directory => directory.CountryCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        // IgnoreQueryFilters: an archived directory still holds the country, the unique index would
        // refuse a second one, and creating a fresh directory would strand the archived
        // arrangement beyond any restore.
        return country is not null
            && await Directories
                .IgnoreQueryFilters()
                .AnyAsync(directory => directory.CountryCode == country, cancellationToken);
    }

    /// <summary>
    /// Which countries still list a category, as codes rather than a count.
    /// <para>
    /// A count would stop the curator without telling them where to go, which turns a two-minute
    /// fix into a hunt through every country on the platform.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<string>> CountriesListingCategoryAsync(
        string knowledgeCategoryKey, CancellationToken cancellationToken = default)
    {
        var key = KnowledgeNormalization.Key(knowledgeCategoryKey);

        if (key is null)
        {
            return [];
        }

        return await Directories
            .AsNoTracking()
            .Where(directory => directory.Entries.Any(entry => entry.KnowledgeCategoryKey == key))
            .OrderBy(directory => directory.CountryCode)
            .Select(directory => directory.CountryCode)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        CountryKnowledgeDirectory directory, CancellationToken cancellationToken = default) =>
        await Directories.AddAsync(directory, cancellationToken);

    /// <inheritdoc cref="KnowledgeCategoryRepository.Remove" />
    public void Remove(CountryKnowledgeDirectory directory) => Directories.Remove(directory);

    private static IQueryable<CountryKnowledgeDirectory> FullGraph(
        IQueryable<CountryKnowledgeDirectory> query) =>
        query
            .Include(directory => directory.Entries)
            .Include(directory => directory.FeaturedGuides)
            .Include(directory => directory.Localizations);
}
