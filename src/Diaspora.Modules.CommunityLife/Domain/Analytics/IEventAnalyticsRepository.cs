using Diaspora.Modules.CommunityLife.Domain.Identifiers;

namespace Diaspora.Modules.CommunityLife.Domain.Analytics;

/// <summary>How the module reads and writes event analytics.</summary>
public interface IEventAnalyticsRepository
{
    Task<EventAnalytics?> FindByEventAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventAnalytics>> FindByOrganizationAsync(
        OrganizationId organizationId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Category totals for a country, over a window. The platform-level statistics the brief asks
    /// for, answered from this table alone rather than by scanning events.
    /// </summary>
    Task<IReadOnlyList<CategoryPerformance>> SummariseByCategoryAsync(
        string? countryCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>The neighbourhoods with the most activity in a window. The popular-areas figure.</summary>
    Task<IReadOnlyList<NeighborhoodActivity>> SummariseByNeighborhoodAsync(
        string? countryCode,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One neighbourhood's totals over a window.
    /// <para>
    /// B12.1. Separate from the ranked summary above rather than filtered out of it: asking for the
    /// top two hundred areas and discarding a hundred and ninety-nine is a scan to answer a question
    /// about one row, and the answer would be silently empty for any area outside the top two
    /// hundred — which is every area that is not one of the busiest on the platform.
    /// </para>
    /// <para>
    /// Null when the area has nothing in the window, which the caller renders as zero rather than as
    /// a not-found: an area with no events is a real answer to "what is happening here".
    /// </para>
    /// </summary>
    Task<NeighborhoodActivity?> SummariseNeighborhoodAsync(
        Guid areaId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task AddAsync(EventAnalytics analytics, CancellationToken cancellationToken = default);

    void Update(EventAnalytics analytics);
}
