using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Projection;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Observability;
using Diaspora.Modules.Analytics.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Projection;

/// <summary>
/// Turns raw facts into daily rollups.
/// <para>
/// <b>One design decision carries the whole of §7, §8, §21, §30 and §31, and it is this: a cell is
/// recomputed from the raw store, never incremented.</b> The projection's job is to notice which
/// cells a batch of new rows touched; the arithmetic is then a fresh <c>GROUP BY</c> over
/// everything in that cell.
/// </para>
/// <list type="bullet">
///   <item>
///     <b>A duplicate has no effect</b>, because the duplicate never became a row — and even if it
///     had, recomputing the cell twice produces the same cell.
///   </item>
///   <item>
///     <b>A late event is correct without special handling.</b> An event that happened three days
///     ago and arrived now is found by the cursor — which walks ingestion order — and lands in a
///     cell that is then rebuilt from scratch. There is no "reopen the closed day" step because no
///     day is ever closed.
///   </item>
///   <item>
///     <b>Out-of-order delivery is not a case.</b> The cell's value does not depend on the order its
///     rows were read in.
///   </item>
///   <item>
///     <b>A backfill is the same operation with a different cell list</b>, which is why there is one
///     rebuild method and not two.
///   </item>
///   <item>
///     <b>A metric definition change is a rebuild</b> rather than a migration, because nothing
///     derived was stored that the new definition would disagree with.
///   </item>
/// </list>
/// <para>
/// The cost is that a busy cell is recomputed on every pass that touches it. That is the trade this
/// platform's scale makes willingly: correctness that does not depend on anybody remembering a
/// compensating step.
/// </para>
/// </summary>
public sealed class AnalyticsProjectionService
{
    private readonly IAnalyticsEventRepository _events;
    private readonly IAnalyticsRollupRepository _rollups;
    private readonly IAnalyticsContentDimensionRepository _dimensions;
    private readonly IAnalyticsProjectionRepository _projection;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AnalyticsReportingCalendar _calendar;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<AnalyticsProjectionService> _logger;

    public AnalyticsProjectionService(
        IAnalyticsEventRepository events,
        IAnalyticsRollupRepository rollups,
        IAnalyticsContentDimensionRepository dimensions,
        IAnalyticsProjectionRepository projection,
        IUnitOfWork unitOfWork,
        AnalyticsReportingCalendar calendar,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsProjectionService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _events = events;
        _rollups = rollups;
        _dimensions = dimensions;
        _projection = projection;
        _unitOfWork = unitOfWork;
        _calendar = calendar;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Folds one batch of new facts into the rollups.
    /// <para>
    /// Returns how many raw rows were read, so a caller can loop until it returns zero. Public
    /// because the integration suite drives it directly rather than waiting for a timer — a test
    /// asserting on a figure after a background loop happens to have fired is a flaky test wearing a
    /// passing badge.
    /// </para>
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var now = _calendar.UtcNow;

        var checkpoint = await _projection.FindCheckpointAsync(cancellationToken).ConfigureAwait(false);

        if (checkpoint is null)
        {
            checkpoint = AnalyticsProjectionCheckpoint.Start();
            _projection.Add(checkpoint);
        }

        var batch = await _events
            .ListAfterAsync(checkpoint.Position, _options.ProjectionBatchSize, cancellationToken)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            checkpoint.Touch(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        // The cursor is an instant and the page was cut at a limit, so the page may have stopped
        // halfway through a group of rows sharing that instant. Advancing past it would skip the
        // rest of the group permanently, so the boundary instant is read whole before the cursor
        // moves. Only when the page was full: a short page has already reached the end of the
        // stream and there is nothing beyond it to have missed.
        if (batch.Count >= _options.ProjectionBatchSize)
        {
            var boundary = batch[^1].ReceivedAt;

            var tie = await _events.ListAtAsync(boundary, cancellationToken).ConfigureAwait(false);

            if (tie.Count > 0)
            {
                var seen = batch.Select(row => row.Id).ToHashSet();

                batch = batch.Concat(tie.Where(row => !seen.Contains(row.Id))).ToList();
            }
        }

        var contentCells = batch
            .Where(row => row.ContentId is not null)
            .Select(row => (row.ContentType, ContentId: row.ContentId!.Value, row.OccurredOn))
            .Distinct()
            .ToList();

        var ownerCells = batch
            .Where(row => row.OwnerUserId is not null)
            .Select(row => (OwnerUserId: row.OwnerUserId!.Value, row.OccurredOn))
            .Distinct()
            .ToList();

        foreach (var cell in contentCells)
        {
            await RebuildContentCellAsync(cell.ContentType, cell.ContentId, cell.OccurredOn, now, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var cell in ownerCells)
        {
            await RebuildOwnerCellAsync(cell.OwnerUserId, cell.OccurredOn, now, cancellationToken)
                .ConfigureAwait(false);
        }

        var last = batch[^1];
        checkpoint.Advance(last.ReceivedAt, last.Id.Value, batch.Count, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        AnalyticsTelemetry.CellsRebuilt.Add(contentCells.Count + ownerCells.Count);
        AnalyticsTelemetry.AggregationDuration.Record(stopwatch.Elapsed.TotalMilliseconds);

        return batch.Count;
    }

    /// <summary>
    /// Rebuilds every cell with activity in a window.
    /// <para>
    /// The backfill's unit of work, and the same rebuild the ordinary pass uses. The window is
    /// bounded by the caller and the caller is bounded by configuration — there is deliberately no
    /// "rebuild everything".
    /// </para>
    /// </summary>
    public async Task<int> RebuildRangeAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default)
    {
        var now = _calendar.UtcNow;

        var contentCells = await _events
            .ListContentCellsAsync(fromDay, toDay, cancellationToken)
            .ConfigureAwait(false);

        foreach (var cell in contentCells)
        {
            await RebuildContentCellAsync(cell.ContentType, cell.ContentId, cell.Day, now, cancellationToken)
                .ConfigureAwait(false);
        }

        var ownerCells = await _events
            .ListOwnerCellsAsync(fromDay, toDay, cancellationToken)
            .ConfigureAwait(false);

        foreach (var cell in ownerCells)
        {
            await RebuildOwnerCellAsync(cell.OwnerUserId, cell.Day, now, cancellationToken)
                .ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var rebuilt = contentCells.Count + ownerCells.Count;

        AnalyticsTelemetry.CellsRebuilt.Add(
            rebuilt, new KeyValuePair<string, object?>("reason", "backfill"));

        return rebuilt;
    }

    /// <summary>
    /// Recomputes one item's day.
    /// <para>
    /// The owner is taken from the dimension where there is one and from the rows otherwise, so a
    /// cell whose content analytics has never seen published still reaches the right creator once
    /// the publication arrives and the rows are attributed.
    /// </para>
    /// </summary>
    private async Task RebuildContentCellAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var counts = await _events
            .AggregateContentDayAsync(contentType, contentId, day, cancellationToken)
            .ConfigureAwait(false);

        var uniqueViewers = await _events
            .CountDistinctViewersForContentDayAsync(contentType, contentId, day, cancellationToken)
            .ConfigureAwait(false);

        var dimension = await _dimensions
            .FindAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        var rollup = await _rollups
            .FindContentAsync(contentType, contentId, day, cancellationToken)
            .ConfigureAwait(false);

        if (rollup is null)
        {
            rollup = ContentDailyRollup.Create(
                ContentDailyRollupId.New(),
                day,
                contentType,
                contentId,
                dimension?.OwnerUserId ?? Guid.Empty);

            _rollups.Add(rollup);
        }

        rollup.Replace(
            ToCounts(counts),
            uniqueViewers,
            dimension?.OwnerUserId ?? Guid.Empty,
            dimension?.CountryCode,
            now);
    }

    /// <summary>Recomputes one owner's day, across everything attributed to them.</summary>
    private async Task RebuildOwnerCellAsync(
        Guid ownerUserId, DateOnly day, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var counts = await _events
            .AggregateOwnerDayAsync(ownerUserId, day, cancellationToken)
            .ConfigureAwait(false);

        var uniqueViewers = await _events
            .CountDistinctViewersForOwnerDayAsync(ownerUserId, day, cancellationToken)
            .ConfigureAwait(false);

        var rollup = await _rollups
            .FindOwnerAsync(ownerUserId, day, cancellationToken)
            .ConfigureAwait(false);

        if (rollup is null)
        {
            rollup = OwnerDailyRollup.Create(OwnerDailyRollupId.New(), day, ownerUserId);
            _rollups.Add(rollup);
        }

        rollup.Replace(ToCounts(counts), uniqueViewers, counts.ProfileViews, now);
    }

    private static AnalyticsCounts ToCounts(AnalyticsCellCounts counts) =>
        new(
            counts.Views,
            counts.Saves,
            counts.Shares,
            counts.Comments,
            counts.Reactions,
            counts.HelpfulVotes,
            counts.Conversions,
            counts.Published,
            counts.FollowsGained,
            counts.FollowsLost);
}
