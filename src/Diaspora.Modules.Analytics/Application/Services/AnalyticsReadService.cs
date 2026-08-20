using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// Reads windows of rollups and turns them into totals.
/// <para>
/// <b>Rollups for the additive counts, the raw store for the distinct ones, and nothing else.</b>
/// §22 of the phase brief asks that a dashboard not scan raw event history; §14 asks that a distinct
/// count not be faked by summing daily ones. Both are kept by knowing which figures are summable —
/// the answer is on <c>AnalyticsCounts</c>, which deliberately has no field that is not — and asking
/// the right store for each.
/// </para>
/// </summary>
public sealed class AnalyticsReadService
{
    private readonly IAnalyticsRollupRepository _rollups;
    private readonly IAnalyticsEventRepository _events;

    public AnalyticsReadService(
        IAnalyticsRollupRepository rollups, IAnalyticsEventRepository events)
    {
        _rollups = rollups;
        _events = events;
    }

    /// <summary>One owner's totals for a window.</summary>
    public async Task<AnalyticsWindowTotals> ReadOwnerAsync(
        Guid ownerUserId, AnalyticsPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var rows = await _rollups
            .ListOwnerAsync(ownerUserId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);

        var counts = rows.Aggregate(AnalyticsCounts.Zero, (total, row) => total.Add(row.Counts));
        var profileViews = rows.Sum(row => row.ProfileViews);

        // Distinct viewers cannot be summed. A reader who came back on three days is one unique
        // viewer and three daily ones; adding the daily figures would publish the second number
        // under the first one's name, and it would be wrong in the flattering direction.
        var uniqueViewers = await _events
            .CountDistinctViewersForOwnerAsync(ownerUserId, period.Start, period.End, cancellationToken)
            .ConfigureAwait(false);

        return new AnalyticsWindowTotals(counts, uniqueViewers, profileViews, rows.Count > 0);
    }

    /// <summary>One item's totals for a window.</summary>
    public async Task<AnalyticsWindowTotals> ReadContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        AnalyticsPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var rows = await _rollups
            .ListContentAsync(contentType, contentId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);

        var counts = rows.Aggregate(AnalyticsCounts.Zero, (total, row) => total.Add(row.Counts));

        var uniqueViewers = await _events
            .CountDistinctViewersForContentAsync(
                contentType, contentId, period.Start, period.End, cancellationToken)
            .ConfigureAwait(false);

        return new AnalyticsWindowTotals(counts, uniqueViewers, 0, rows.Count > 0);
    }

    /// <summary>
    /// One owner's daily rows for a window, for the series.
    /// <para>
    /// Returned as a dictionary rather than a list because the bucketer walks the period's calendar
    /// and asks for each day: a day with no activity has no row, and a series that iterated the rows
    /// instead would silently drop the gaps and draw a line through them.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<OwnerDailyRollup>> ReadOwnerDailyAsync(
        Guid ownerUserId, AnalyticsPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        return await _rollups
            .ListOwnerAsync(ownerUserId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Every content row for one owner in a window. The per-item breakdown's source.</summary>
    public async Task<IReadOnlyList<ContentDailyRollup>> ReadOwnerContentDailyAsync(
        Guid ownerUserId, AnalyticsPeriod period, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        return await _rollups
            .ListContentForOwnerAsync(ownerUserId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>One item's daily rows for a window.</summary>
    public async Task<IReadOnlyList<ContentDailyRollup>> ReadContentDailyAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        AnalyticsPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        return await _rollups
            .ListContentAsync(contentType, contentId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);
    }
}
