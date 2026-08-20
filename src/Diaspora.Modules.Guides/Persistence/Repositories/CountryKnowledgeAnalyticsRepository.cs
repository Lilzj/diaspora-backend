using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>EF Core repository for the per-country usage counters.</summary>
public sealed class CountryKnowledgeAnalyticsRepository : ICountryKnowledgeAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CountryKnowledgeAnalyticsRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<CountryKnowledgeAnalytics> Analytics =>
        _dbContext.Set<CountryKnowledgeAnalytics>();

    public async Task<CountryKnowledgeAnalytics?> GetAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await Analytics
                .Include(analytics => analytics.CategoryBreakdown)
                .FirstOrDefaultAsync(
                    analytics => analytics.CountryCode == country, cancellationToken);
    }

    public async Task<CountryKnowledgeAnalytics?> GetForReadAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        return country is null
            ? null
            : await Analytics
                .AsNoTracking()
                .Include(analytics => analytics.CategoryBreakdown)
                .FirstOrDefaultAsync(
                    analytics => analytics.CountryCode == country, cancellationToken);
    }

    /// <summary>
    /// The leaderboard behind the explore grid's default ordering.
    /// <para>
    /// No breakdown loaded: the grid renders a country name and a view count, and including the
    /// per-category rows for a hundred and ninety-five countries to sort by one column would be the
    /// same mistake the directory repository avoids with its header shape.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CountryKnowledgeAnalytics>> TopByInterestAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Analytics
            .AsNoTracking()
            .OrderByDescending(analytics => analytics.DirectoryViews)
            .ThenBy(analytics => analytics.CountryCode)
            .Take(Math.Clamp(limit, 1, 250))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        CountryKnowledgeAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);
}
