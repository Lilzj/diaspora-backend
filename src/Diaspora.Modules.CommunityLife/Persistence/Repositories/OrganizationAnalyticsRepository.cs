using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>EF Core repository for organization analytics.</summary>
public sealed class OrganizationAnalyticsRepository : IOrganizationAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OrganizationAnalyticsRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<OrganizationAnalytics> Analytics => _dbContext.Set<OrganizationAnalytics>();

    public async Task<OrganizationAnalytics?> FindByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        return await Analytics.FirstOrDefaultAsync(
            analytics => analytics.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationAnalytics>> FindManyAsync(
        IReadOnlyCollection<OrganizationId> organizationIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);

        if (organizationIds.Count == 0)
        {
            return [];
        }

        var wanted = organizationIds.Distinct().ToList();

        return await Analytics
            .AsNoTracking()
            .Where(analytics => wanted.Contains(analytics.OrganizationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationTypePerformance>> SummariseByTypeAsync(
        string? countryCode, CancellationToken cancellationToken = default)
    {
        var query = Analytics.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(analytics => analytics.CountryCode == country);
        }

        var rows = await query
            .GroupBy(analytics => analytics.OrganizationTypeKey)
            .Select(group => new
            {
                TypeKey = group.Key,
                OrganizationCount = group.Count(),
                TotalMembers = group.Sum(analytics => analytics.CurrentMemberCount),
                TotalJoins = group.Sum(analytics => analytics.TotalJoins),
                TotalDepartures = group.Sum(analytics => analytics.TotalDepartures),
                EventsPublished = group.Sum(analytics => analytics.EventsPublished),
                EventsCompleted = group.Sum(analytics => analytics.EventsCompleted)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new OrganizationTypePerformance(
                row.TypeKey,
                row.OrganizationCount,
                row.TotalMembers,
                row.TotalJoins,
                row.TotalDepartures,
                row.EventsPublished,
                row.EventsCompleted))
        ];
    }

    public async Task AddAsync(
        OrganizationAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);

    public void Update(OrganizationAnalytics analytics) => Analytics.Update(analytics);
}
