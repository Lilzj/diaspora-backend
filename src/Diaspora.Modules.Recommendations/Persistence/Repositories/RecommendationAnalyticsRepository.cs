using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the analytics rollup.
/// <para>
/// <b><see cref="GetOrOpenAsync"/> checks the change tracker first, and here the trap is unusually
/// easy to hit.</b> One rollup pass folds hundreds of feedback rows into a handful of day-and-surface
/// rows, and each fold asks for its row. A second database query would find nothing for a row
/// added-but-unsaved and add a duplicate, which the unique index rejects — failing the whole pass and
/// leaving the day's analytics permanently unfolded because the retry hits the same thing.
/// </para>
/// </summary>
public sealed class RecommendationAnalyticsRepository : IRecommendationAnalyticsRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RecommendationAnalyticsRepository(
        DiasporaDbContext dbContext, IGuidGenerator guids, IClock clock)
    {
        _dbContext = dbContext;
        _guids = guids;
        _clock = clock;
    }

    private DbSet<RecommendationAnalytics> Analytics => _dbContext.Set<RecommendationAnalytics>();

    public void Add(RecommendationAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        Analytics.Add(analytics);
    }

    public async Task<RecommendationAnalytics?> FindAsync(
        DateOnly day, RecommendationSurface surface, CancellationToken cancellationToken = default) =>
        await Analytics
            .Include(analytics => analytics.Breakdowns)
            .FirstOrDefaultAsync(
                analytics => analytics.Day == day && analytics.Surface == surface, cancellationToken);

    public async Task<RecommendationAnalytics> GetOrOpenAsync(
        DateOnly day, RecommendationSurface surface, CancellationToken cancellationToken = default)
    {
        // The change tracker first. See the class summary — this is correctness, not speed.
        var tracked = _dbContext.ChangeTracker
            .Entries<RecommendationAnalytics>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(analytics => analytics.Day == day && analytics.Surface == surface);

        if (tracked is not null)
        {
            return tracked;
        }

        if (await FindAsync(day, surface, cancellationToken) is { } existing)
        {
            return existing;
        }

        var created = RecommendationAnalytics.Open(
            RecommendationAnalyticsId.From(_guids.NewSequentialGuid()), day, surface, _clock.UtcNow);

        await Analytics.AddAsync(created, cancellationToken);

        return created;
    }

    public async Task<IReadOnlyList<RecommendationAnalytics>> SearchAsync(
        RecommendationAnalyticsFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Analytics
            .Include(analytics => analytics.Breakdowns)
            .AsQueryable();

        if (filter.Surface is { } surface)
        {
            query = query.Where(analytics => analytics.Surface == surface);
        }

        if (filter.From is { } from)
        {
            query = query.Where(analytics => analytics.Day >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(analytics => analytics.Day <= to);
        }

        return await query
            .OrderByDescending(analytics => analytics.Day)
            .ThenBy(analytics => analytics.Surface)
            .Take(Math.Max(1, filter.Limit))
            .ToListAsync(cancellationToken);
    }

    public void Update(RecommendationAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        RecommendationAggregateUpdater.MarkDirty(_dbContext, analytics);
    }

    /// <summary>
    /// Retention.
    /// <para>
    /// Loaded and removed rather than executed as a delete, because the breakdown rows are an owned
    /// child table and only the change tracker knows to take them with it. Rollups are small and
    /// kept far longer than the rows behind them, so this deletes very little very rarely.
    /// </para>
    /// </summary>
    public async Task<int> DeleteOlderThanAsync(
        DateOnly before, CancellationToken cancellationToken = default)
    {
        var doomed = await Analytics
            .Include(analytics => analytics.Breakdowns)
            .Where(analytics => analytics.Day < before)
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Analytics.RemoveRange(doomed);

        return doomed.Count;
    }
}
