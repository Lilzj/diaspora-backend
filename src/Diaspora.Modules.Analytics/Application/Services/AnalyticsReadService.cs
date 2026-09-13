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

        // Asked only of an empty window: a window with rows has history by definition.
        var hasHistory = rows.Count > 0
            || await _rollups
                .AnyOwnerBeforeAsync(ownerUserId, period.FromDate, cancellationToken)
                .ConfigureAwait(false);

        return new AnalyticsWindowTotals(counts, uniqueViewers, profileViews, rows.Count > 0, hasHistory);
    }

    /// <summary>
    /// One owner's totals for a window, for one type of content only.
    /// <para>
    /// <b>Summed from the per-item rows, not read from the owner rollup.</b> The owner rollup has
    /// already added every type together and cannot be taken apart again; the per-item rows carry
    /// their type, so narrowing them and summing is the only way to a seller's figure that does not
    /// include their guides. Profile views are zero here on purpose: a profile is not a listing.
    /// </para>
    /// </summary>
    public async Task<AnalyticsWindowTotals> ReadOwnerContentTypeAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        AnalyticsPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        var rows = await ReadOwnerContentTypeDailyAsync(ownerUserId, contentType, period, cancellationToken)
            .ConfigureAwait(false);

        var counts = rows.Aggregate(AnalyticsCounts.Zero, (total, row) => total.Add(row.Counts));

        var uniqueViewers = await _events
            .CountDistinctViewersForOwnerContentTypeAsync(
                ownerUserId, contentType, period.Start, period.End, cancellationToken)
            .ConfigureAwait(false);

        // Narrowed like the counts. The owner-wide check would count a member's guides as history
        // for listings they published this morning.
        var hasHistory = rows.Count > 0
            || await _rollups
                .AnyOwnerContentTypeBeforeAsync(ownerUserId, contentType, period.FromDate, cancellationToken)
                .ConfigureAwait(false);

        return new AnalyticsWindowTotals(counts, uniqueViewers, 0, rows.Count > 0, hasHistory);
    }

    /// <summary>One owner's per-item rows for one type of content in a window.</summary>
    public async Task<IReadOnlyList<ContentDailyRollup>> ReadOwnerContentTypeDailyAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        AnalyticsPeriod period,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(period);

        // Narrowed in memory: one owner's rows over one period is small, and adding a type
        // parameter to the rollup query would be a second index for a filter this cheap.
        var rows = await _rollups
            .ListContentForOwnerAsync(ownerUserId, period.FromDate, period.ToDate, cancellationToken)
            .ConfigureAwait(false);

        return rows.Where(row => row.ContentType == contentType).ToList();
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

        var hasHistory = rows.Count > 0
            || await _rollups
                .AnyContentBeforeAsync(contentType, contentId, period.FromDate, cancellationToken)
                .ConfigureAwait(false);

        return new AnalyticsWindowTotals(counts, uniqueViewers, 0, rows.Count > 0, hasHistory);
    }

    /// <summary>
    /// Which of one owner's items analytics had recorded anything for before a day.
    /// <para>
    /// The per-item rows' history, in one read for the whole page. A set, because the only question
    /// a row asks of it is whether its own item is in it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlySet<(AnalyticsContentType ContentType, Guid ContentId)>> ReadOwnerContentRecordedBeforeAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken)
    {
        var keys = await _rollups
            .ListContentRecordedBeforeAsync(ownerUserId, day, cancellationToken)
            .ConfigureAwait(false);

        return keys.ToHashSet();
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
