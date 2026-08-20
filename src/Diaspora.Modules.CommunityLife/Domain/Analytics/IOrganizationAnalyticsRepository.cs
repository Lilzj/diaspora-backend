using Diaspora.Modules.CommunityLife.Domain.Identifiers;

namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>How the module reads and writes organization analytics.</summary>
public interface IOrganizationAnalyticsRepository
{
    Task<OrganizationAnalytics?> FindByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrganizationAnalytics>> FindManyAsync(
        IReadOnlyCollection<OrganizationId> organizationIds,
        CancellationToken cancellationToken = default);

    /// <summary>Organization growth totals for a country. The directory-level statistics.</summary>
    Task<IReadOnlyList<OrganizationTypePerformance>> SummariseByTypeAsync(
        string? countryCode, CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationAnalytics analytics, CancellationToken cancellationToken = default);

    void Update(OrganizationAnalytics analytics);
}
