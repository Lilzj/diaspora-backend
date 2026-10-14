using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for event analytics.
/// <para>
/// The two summary reads are grouped aggregates computed in the database. Doing them any other way
/// — loading the rows and grouping in memory — would work perfectly on a test deployment and fall
/// over on the first one with a year of events in it.
/// </para>
/// </summary>
public sealed class EventAnalyticsRepository : IEventAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;

    public EventAnalyticsRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<EventAnalytics> Analytics => _dbContext.Set<EventAnalytics>();

    public async Task<EventAnalytics?> FindByEventAsync(
        CommunityEventId eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventId);

        return await Analytics.FirstOrDefaultAsync(
            analytics => analytics.EventId == eventId, cancellationToken);
    }

    public async Task<IReadOnlyList<EventAnalytics>> FindByOrganizationAsync(
        OrganizationId organizationId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        return await Analytics
            .AsNoTracking()
            .Where(analytics => analytics.OrganizationId == organizationId)
            .OrderByDescending(analytics => analytics.OpenedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryPerformance>> SummariseByCategoryAsync(
        string? countryCode,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var query = Analytics
            .AsNoTracking()
            .Where(analytics => analytics.OpenedAt >= from && analytics.OpenedAt <= to);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(analytics => analytics.CountryCode == country);
        }

        var rows = await query
            .GroupBy(analytics => analytics.CategoryKey)
            .Select(group => new
            {
                CategoryKey = group.Key,
                EventCount = group.Count(),
                PublishedCount = group.Count(analytics => analytics.PublishedAt != null),
                CompletedCount = group.Count(analytics => analytics.CompletedAt != null),
                TotalRsvps = group.Sum(analytics => analytics.RsvpCount),
                TotalAttendances = group.Sum(analytics => analytics.AttendedCount),
                TotalNoShows = group.Sum(analytics => analytics.NoShowCount),
                TotalViews = group.Sum(analytics => analytics.UniqueViewCount)
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new CategoryPerformance(
                row.CategoryKey,
                row.EventCount,
                row.PublishedCount,
                row.CompletedCount,

                // Cancellations are not counted here: an analytics row does not record a
                // cancellation, and inferring one from "published but never completed" would also
                // catch every event still in the future.
                0,
                row.TotalRsvps,
                row.TotalAttendances,
                row.TotalNoShows,
                row.TotalViews))
        ];
    }

    public async Task<IReadOnlyList<NeighborhoodActivity>> SummariseByNeighborhoodAsync(
        string? countryCode,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Analytics
            .AsNoTracking()
            .Where(analytics => analytics.NeighborhoodAreaId != null
                && analytics.OpenedAt >= from
                && analytics.OpenedAt <= to);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(analytics => analytics.CountryCode == country);
        }

        var rows = await query
            .GroupBy(analytics => analytics.NeighborhoodAreaId!.Value)
            .Select(group => new
            {
                AreaId = group.Key,
                EventCount = group.Count(),
                TotalRsvps = group.Sum(analytics => analytics.RsvpCount),
                TotalAttendances = group.Sum(analytics => analytics.AttendedCount)
            })
            .OrderByDescending(row => row.EventCount)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new NeighborhoodActivity(
                row.AreaId, row.EventCount, row.TotalRsvps, row.TotalAttendances))
        ];
    }

    /// <summary>
    /// One neighbourhood's totals. An indexed equality on the area rather than a grouped scan of
    /// every area on the platform.
    /// </summary>
    public async Task<IReadOnlyList<EventActivityPoint>> FindActivityWithinBoxAsync(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        DateTimeOffset from,
        DateTimeOffset to,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Joined to the event for the venue's coordinate, which the analytics row does not copy. The
        // same publication rule as every public read: published or since run, public, not hidden.
        EventStatus[] happened = [EventStatus.Published, EventStatus.InProgress, EventStatus.Completed];

        return await Analytics
            .AsNoTracking()
            .Where(analytics => analytics.OpenedAt >= from && analytics.OpenedAt <= to)
            .Join(
                _dbContext.Set<CommunityEvent>().AsNoTracking(),
                analytics => analytics.EventId,
                communityEvent => communityEvent.Id,
                (analytics, communityEvent) => new { analytics, communityEvent })
            .Where(row =>
                happened.Contains(row.communityEvent.Status)
                && row.communityEvent.Visibility == EventVisibility.Public
                && row.communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && row.communityEvent.ModerationState != CommunityLifeModerationState.Suspended
                && row.communityEvent.Venue.Latitude != null
                && row.communityEvent.Venue.Longitude != null
                && row.communityEvent.Venue.Latitude >= minLatitude
                && row.communityEvent.Venue.Latitude <= maxLatitude
                && row.communityEvent.Venue.Longitude >= minLongitude
                && row.communityEvent.Venue.Longitude <= maxLongitude)
            .OrderByDescending(row => row.analytics.OpenedAt)
            .Take(Math.Clamp(limit, 1, 2_000))
            .Select(row => new EventActivityPoint(
                row.communityEvent.Venue.Latitude!.Value,
                row.communityEvent.Venue.Longitude!.Value,
                row.analytics.RsvpCount,
                row.analytics.AttendedCount))
            .ToListAsync(cancellationToken);
    }

    public async Task<NeighborhoodActivity?> SummariseNeighborhoodAsync(
        Guid areaId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (areaId == Guid.Empty)
        {
            return null;
        }

        var row = await Analytics
            .AsNoTracking()
            .Where(analytics => analytics.NeighborhoodAreaId == areaId
                && analytics.OpenedAt >= from
                && analytics.OpenedAt <= to)
            .GroupBy(analytics => analytics.NeighborhoodAreaId!.Value)
            .Select(group => new
            {
                EventCount = group.Count(),
                TotalRsvps = group.Sum(analytics => analytics.RsvpCount),
                TotalAttendances = group.Sum(analytics => analytics.AttendedCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new NeighborhoodActivity(
                areaId, row.EventCount, row.TotalRsvps, row.TotalAttendances);
    }

    public async Task AddAsync(
        EventAnalytics analytics, CancellationToken cancellationToken = default) =>
        await Analytics.AddAsync(analytics, cancellationToken);

    public void Update(EventAnalytics analytics) => Analytics.Update(analytics);
}
