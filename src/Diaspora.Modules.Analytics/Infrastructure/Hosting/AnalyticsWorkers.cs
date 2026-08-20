using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Application.Projection;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Observability;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Operations.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Infrastructure.Hosting;

/// <summary>
/// Runs the rollup projection on a timer.
/// <para>
/// <b>Safe under horizontal scaling, and the safety has two independent halves.</b> B17's
/// <see cref="IDistributedLock"/> means only one instance runs a pass — and if the lock is the
/// in-process default, or the lease is lost mid-pass, the projection is still correct, because a
/// cell rebuilt twice produces the same cell. §29 asks for a worker that does not assume one
/// instance; this one does not even assume the lock works.
/// </para>
/// <para>
/// <b>What it does <em>not</em> do is block anything.</b> §56: analytics must not break the
/// originating business operation. A guide is published, the outbox delivers the contract, a
/// consumer stages a row, and this loop turns it into a figure some seconds later. If every part of
/// that fails, the guide is still published.
/// </para>
/// <para>
/// <b>Off in the integration suite, switchable from the module's first day.</b> DEBT-049 recorded
/// the cost of a background loop querying the suite's one shared SQLite connection three separate
/// times; the switch exists before the first test, and the tests drive
/// <see cref="AnalyticsProjectionService.RunOnceAsync"/> directly.
/// </para>
/// </summary>
public sealed class AnalyticsProjectionWorker : BackgroundService
{
    private const string LockKey = "analytics:projection";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AnalyticsOptions> _options;
    private readonly ILogger<AnalyticsProjectionWorker> _logger;

    public AnalyticsProjectionWorker(
        IServiceScopeFactory scopes,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsProjectionWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableProjectionSweep)
        {
            _logger.LogInformation("AnalyticsProjectionWorker is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.Value.ProjectionIntervalSeconds));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// One pass, draining up to a bounded number of batches.
    /// <para>
    /// Bounded rather than "until empty", because a projection that drained an unbounded backlog
    /// inside one lease would hold the lease past its time-to-live and be overtaken by a second
    /// instance mid-pass. Draining a few batches per tick catches up quickly and stays inside the
    /// lease.
    /// </para>
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        const int MaximumBatchesPerPass = 10;

        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(
                LockKey,
                TimeSpan.FromSeconds(Math.Max(60, _options.Value.ProjectionIntervalSeconds * 4)),
                cancellationToken).ConfigureAwait(false);

        if (locks is not null && lease is null)
        {
            _logger.LogDebug("Another instance holds the analytics projection lease.");

            return;
        }

        try
        {
            var projection = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();

            for (var pass = 0; pass < MaximumBatchesPerPass; pass++)
            {
                var processed = await projection.RunOnceAsync(cancellationToken).ConfigureAwait(false);

                if (processed == 0)
                {
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AnalyticsTelemetry.AggregationFailures.Add(1);

            // Swallowed and counted rather than rethrown, because a BackgroundService that throws
            // takes the host down with it. The rollups are stale by exactly one interval and the
            // next pass reads the same cursor, so a transient fault costs nothing.
            _logger.LogError(exception, "An analytics projection pass failed.");
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Deletes what the retention policy says may no longer be kept.
/// <para>
/// <b>Two windows, and the gap between them is the design.</b> Raw events are kept for ninety days
/// — long enough to recompute, backfill or correct any figure the platform serves, and no longer,
/// because a raw row is a pseudonymous record of one person's behaviour. Daily rollups are kept for
/// twenty-five months, because a year-on-year comparison needs a whole month to compare against and
/// a rollup is a count with nobody in it.
/// </para>
/// <para>
/// <b>What that costs, stated rather than hidden:</b> a figure older than the raw window cannot be
/// recomputed. If a metric definition changes, history before the window keeps the old definition,
/// and the served definition version is what makes that visible rather than silent.
/// </para>
/// </summary>
public sealed class AnalyticsRetentionWorker : BackgroundService
{
    private const string LockKey = "analytics:retention";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AnalyticsOptions> _options;
    private readonly ILogger<AnalyticsRetentionWorker> _logger;

    public AnalyticsRetentionWorker(
        IServiceScopeFactory scopes,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsRetentionWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableRetentionSweep)
        {
            _logger.LogInformation("AnalyticsRetentionWorker is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(60, _options.Value.RetentionIntervalSeconds));

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>One pass. Public so the suite can drive it deterministically.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(LockKey, TimeSpan.FromMinutes(10), cancellationToken)
                .ConfigureAwait(false);

        if (locks is not null && lease is null)
        {
            return 0;
        }

        try
        {
            var options = _options.Value;
            var calendar = scope.ServiceProvider.GetRequiredService<AnalyticsReportingCalendar>();
            var events = scope.ServiceProvider.GetRequiredService<IAnalyticsEventRepository>();
            var rollups = scope.ServiceProvider.GetRequiredService<IAnalyticsRollupRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var now = calendar.UtcNow;

            var deletedEvents = await events
                .DeleteExpiredAsync(
                    now.AddDays(-options.RawEventRetentionDays),
                    options.RetentionBatchSize,
                    cancellationToken)
                .ConfigureAwait(false);

            var deletedRollups = await rollups
                .DeleteExpiredAsync(
                    calendar.Today.AddDays(-options.RollupRetentionDays),
                    options.RetentionBatchSize,
                    cancellationToken)
                .ConfigureAwait(false);

            if (deletedEvents + deletedRollups == 0)
            {
                return 0;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AnalyticsTelemetry.RowsDeleted.Add(
                deletedEvents, new KeyValuePair<string, object?>("reason", "retention"));

            AnalyticsTelemetry.RowsDeleted.Add(
                deletedRollups,
                new KeyValuePair<string, object?>("reason", "retention"),
                new KeyValuePair<string, object?>("table", "rollup"));

            return deletedEvents + deletedRollups;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "An analytics retention pass failed.");

            return 0;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

/// <summary>
/// Runs administrative backfills, one slice at a time.
/// <para>
/// <b>Separate from the projection worker on purpose.</b> A rebuild of ninety days is a long
/// operation and putting it inside the loop that keeps today's figures current would make an
/// operator's decision to correct history stop the platform reporting the present.
/// </para>
/// </summary>
public sealed class AnalyticsBackfillWorker : BackgroundService
{
    private const string LockKey = "analytics:backfill";

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<AnalyticsOptions> _options;
    private readonly ILogger<AnalyticsBackfillWorker> _logger;

    public AnalyticsBackfillWorker(
        IServiceScopeFactory scopes,
        IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsBackfillWorker> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableProjectionSweep)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Advances the active run by one slice.
    /// <para>
    /// Resumable because the run stores where it got to: a killed process leaves a run whose
    /// <c>NextDay</c> is the first day not yet rebuilt, and the next pass picks it up there rather
    /// than starting again.
    /// </para>
    /// </summary>
    public async Task<bool> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var locks = scope.ServiceProvider.GetService<IDistributedLock>();

        var lease = locks is null
            ? null
            : await locks.TryAcquireAsync(LockKey, TimeSpan.FromMinutes(15), cancellationToken)
                .ConfigureAwait(false);

        if (locks is not null && lease is null)
        {
            return false;
        }

        try
        {
            var runs = scope.ServiceProvider.GetRequiredService<IAnalyticsProjectionRepository>();
            var run = await runs.FindActiveRunAsync(cancellationToken).ConfigureAwait(false);

            if (run is null)
            {
                return false;
            }

            var projection = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();
            var calendar = scope.ServiceProvider.GetRequiredService<AnalyticsReportingCalendar>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            run.Start(calendar.UtcNow);

            var slice = Math.Max(1, _options.Value.BackfillSliceDays);
            var from = run.NextDay;
            var to = from.AddDays(slice - 1);

            if (to > run.ToDay)
            {
                to = run.ToDay;
            }

            var rebuilt = await projection
                .RebuildRangeAsync(from, to, cancellationToken)
                .ConfigureAwait(false);

            run.AdvanceTo(to.AddDays(1), rebuilt);

            if (run.NextDay > run.ToDay)
            {
                run.Complete(calendar.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            AnalyticsTelemetry.BackfillProgress.Add(to.DayNumber - from.DayNumber + 1);

            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "An analytics backfill slice failed.");

            await FailActiveRunAsync(scope.ServiceProvider, exception, cancellationToken)
                .ConfigureAwait(false);

            return false;
        }
        finally
        {
            if (lease is not null)
            {
                await lease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Marks the run failed so it stops being retried forever.
    /// <para>
    /// <b>The message is the exception's, not a stack trace.</b> A failure reason an operator reads
    /// should say what went wrong; a stack trace in a stored field is a copy of the log with none of
    /// its context and a longer retention.
    /// </para>
    /// </summary>
    private static async Task FailActiveRunAsync(
        IServiceProvider provider, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var runs = provider.GetRequiredService<IAnalyticsProjectionRepository>();
            var calendar = provider.GetRequiredService<AnalyticsReportingCalendar>();
            var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

            var run = await runs.FindActiveRunAsync(cancellationToken).ConfigureAwait(false);

            if (run is null)
            {
                return;
            }

            run.Fail(exception.Message, calendar.UtcNow);

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The run stays Running and the next pass tries again. Losing the failure note is
            // better than throwing out of the catch that was handling the first failure.
        }
    }
}
