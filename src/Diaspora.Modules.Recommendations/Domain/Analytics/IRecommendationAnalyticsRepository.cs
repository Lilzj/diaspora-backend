using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Domain.Analytics;

/// <summary>
/// The analytics rollup store.
/// <para>
/// <b><see cref="GetOrOpenAsync"/> checks the change tracker first, for the reason every
/// get-or-create in this platform does.</b> One rollup pass folds hundreds of feedback rows into a
/// handful of day-and-surface rows, and each fold asks for its row. A second database query would
/// find nothing for a row added-but-unsaved and add a duplicate the unique index rejects, failing
/// the whole pass.
/// </para>
/// </summary>
public interface IRecommendationAnalyticsRepository
{
    void Add(RecommendationAnalytics analytics);

    Task<RecommendationAnalytics?> FindAsync(
        DateOnly day, RecommendationSurface surface, CancellationToken cancellationToken = default);

    /// <summary>The day's row for a surface, created on first fold. See the class summary.</summary>
    Task<RecommendationAnalytics> GetOrOpenAsync(
        DateOnly day, RecommendationSurface surface, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecommendationAnalytics>> SearchAsync(
        RecommendationAnalyticsFilter filter, CancellationToken cancellationToken = default);

    void Update(RecommendationAnalytics analytics);

    /// <summary>Retention. Rollups are small and kept far longer than the rows behind them.</summary>
    Task<int> DeleteOlderThanAsync(DateOnly before, CancellationToken cancellationToken = default);
}
