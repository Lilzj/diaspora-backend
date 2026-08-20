using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>EF Core repository for the cross-vertical counter projection.</summary>
public sealed class CountryContextSummaryRepository : ICountryContextSummaryRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryContextSummaryRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CountryContextSummary> Summaries => _dbContext.Set<CountryContextSummary>();

    public async Task<CountryContextSummary?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await Summaries
                .Include(summary => summary.Counters)
                .FirstOrDefaultAsync(summary => summary.CountryCode == country, cancellationToken);
    }

    public async Task<CountryContextSummary?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await Summaries
                .AsNoTracking()
                .Include(summary => summary.Counters)
                .FirstOrDefaultAsync(summary => summary.CountryCode == country, cancellationToken);
    }

    /// <summary>
    /// Counters for several countries at once.
    /// <para>
    /// The N+1 guard for the country grid, which renders a badge per country and would otherwise be
    /// one query per tile — a hundred and ninety-five round trips to draw one screen.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, CountryContextSummary>> GetManyForReadAsync(
        IReadOnlyCollection<string> countryCodes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countryCodes);

        var countries = countryCodes
            .Select(KnowledgeNormalization.Country)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (countries.Count == 0)
        {
            return new Dictionary<string, CountryContextSummary>(StringComparer.Ordinal);
        }

        var found = await Summaries
            .AsNoTracking()
            .Include(summary => summary.Counters)
            .Where(summary => countries.Contains(summary.CountryCode))
            .ToListAsync(cancellationToken);

        return found.ToDictionary(summary => summary.CountryCode, StringComparer.Ordinal);
    }

    public async Task AddAsync(
        CountryContextSummary summary, CancellationToken cancellationToken = default) =>
        await Summaries.AddAsync(summary, cancellationToken);
}
