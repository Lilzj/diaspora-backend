using Diaspora.Platform.Messaging.Outbox.Dispatch;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// Runs the dispatcher on a schedule.
/// <para>
/// A hosted service rather than a job on <c>IBackgroundJobScheduler</c>, and the choice is
/// worth stating. That abstraction exists and has no implementation behind it — no Hangfire, no
/// Quartz, nothing that would actually run a recurring job — so scheduling the outbox on it
/// would mean the platform's durability guarantee depended on a component nobody has built.
/// The interface is also the wrong shape for this: it schedules serializable job payloads,
/// while the dispatcher needs a continuously running loop that reacts to queue depth. When a
/// real scheduler arrives, it can drive <see cref="IOutboxDispatcher.DispatchOnceAsync"/>
/// directly and this class goes away; the split between draining and scheduling was made with
/// exactly that in mind.
/// </para>
/// <para>
/// <b>A full batch is followed immediately.</b> Sleeping the poll interval after every pass
/// would drain a backlog of ten thousand at one batch per second regardless of how fast the
/// database could hand them over. The interval is an idle timer, not a rate limit.
/// </para>
/// </summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly OutboxGaugeRegistration _gauges;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;

    private DateTimeOffset _lastLeaseRecovery = DateTimeOffset.MinValue;
    private DateTimeOffset _lastStatisticsRefresh = DateTimeOffset.MinValue;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopes,
        OutboxGaugeRegistration gauges,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;

        // Taken as a dependency purely to force construction. The observable gauges are created
        // in its constructor, and an observable instrument with no live reference stops
        // reporting after a garbage collection — silently, days later.
        _gauges = gauges;

        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dispatch = _options.Dispatch;

        if (!_options.Enabled || !dispatch.Enabled)
        {
            _logger.LogWarning(
                "The outbox dispatcher is not running on this instance " +
                "[Outbox enabled: {OutboxEnabled}, Dispatch enabled: {DispatchEnabled}]. " +
                "Messages will accumulate until an instance that dispatches drains them.",
                _options.Enabled, dispatch.Enabled);

            return;
        }

        // A short delay so the first pass does not compete with migrations, seeders and the
        // opening burst of traffic — the same courtesy the media and community sweepers extend.
        await DelayAsync(TimeSpan.FromSeconds(dispatch.StartupDelaySeconds), stoppingToken);

        _logger.LogInformation(
            "Outbox dispatcher started [Batch: {BatchSize}, Poll: {PollMs}ms, Lease: {LeaseSeconds}s, " +
            "Ordering: {Ordering}, Gauges: {GaugeCount}].",
            dispatch.BatchSize, dispatch.PollIntervalMilliseconds, dispatch.LeaseSeconds,
            dispatch.Ordering, _gauges.Instruments.Count);

        var idleInterval = TimeSpan.FromMilliseconds(dispatch.PollIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var drainedFully = false;

            try
            {
                drainedFully = await RunPassAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031 // A failed pass must never stop the loop.
            catch (Exception exception)
            {
                // Almost always the database being unreachable. Every message is still on disk
                // and every lease still expires, so the correct response is to log and try
                // again — a dispatcher that exited here would leave the queue unattended until
                // somebody noticed the absence of an error.
                _logger.LogError(
                    exception, "An outbox dispatch pass failed. The loop continues.");
            }
#pragma warning restore CA1031

            if (drainedFully)
            {
                // The batch was full, so there is more waiting. Straight round again.
                continue;
            }

            if (!await DelayAsync(idleInterval, stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation("Outbox dispatcher stopped.");
    }

    /// <summary>
    /// One pass, plus the periodic housekeeping that shares its loop.
    /// </summary>
    /// <returns>Whether the pass took a full batch and should be followed immediately.</returns>
    private async Task<bool> RunPassAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopes.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();

        var now = DateTimeOffset.UtcNow;

        // Runs on the dispatch loop rather than its own timer so a single-instance deployment
        // that crashed recovers without an operator and without a second hosted service.
        if (now - _lastLeaseRecovery >= TimeSpan.FromSeconds(_options.Dispatch.LeaseRecoveryIntervalSeconds))
        {
            _lastLeaseRecovery = now;
            await dispatcher.RecoverExpiredLeasesAsync(stoppingToken);
        }

        var result = await dispatcher.DispatchOnceAsync(stoppingToken);

        if (now - _lastStatisticsRefresh >= TimeSpan.FromSeconds(_options.Health.SnapshotCacheSeconds))
        {
            _lastStatisticsRefresh = now;
            await dispatcher.RefreshStatisticsAsync(stoppingToken);
        }

        return result.WasFull(_options.Dispatch.BatchSize);
    }

    /// <summary>
    /// Gives an in-flight pass a bounded window to finish before the host tears the scope down.
    /// <para>
    /// Not required for correctness — an abandoned message stays leased and another instance
    /// takes it when the lease lapses. It is required for a rolling deployment not to leave a
    /// lease-length gap in delivery latency on every pod recycle.
    /// </para>
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        var drain = TimeSpan.FromSeconds(_options.Dispatch.ShutdownDrainSeconds);

        if (drain > TimeSpan.Zero)
        {
            _logger.LogInformation(
                "Outbox dispatcher stopping; allowing up to {Seconds}s for the current pass.",
                _options.Dispatch.ShutdownDrainSeconds);

            using var drainWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            drainWindow.CancelAfter(drain);

            await base.StopAsync(drainWindow.Token);
            return;
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>Waits, and reports whether the wait completed rather than being cancelled.</summary>
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return !cancellationToken.IsCancellationRequested;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
