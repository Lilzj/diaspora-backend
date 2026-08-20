using System.Diagnostics;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations.Caching;

/// <summary>
/// Runs every registered <see cref="ICacheWarmer"/> once at startup.
/// <para>
/// Warming exists for the cold-replica problem: an instance joining a pool under load misses on the
/// same reference data a hundred times before the cache fills, and the orchestrator responds to the
/// resulting latency by adding another cold instance.
/// </para>
/// <para>
/// <b>It cannot fail startup and it cannot hang it.</b> The whole pass runs under one timeout and
/// every warmer's exception is logged and stepped over. That follows the shape the platform already
/// established for reference-data seeding, and it is not defensiveness for its own sake — an
/// instance that has not warmed is an instance that is slightly slower, and an instance that will
/// not start is an outage.
/// </para>
/// </summary>
public sealed class CacheWarmupHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly CacheOptions _options;
    private readonly ILogger<CacheWarmupHostedService> _logger;

    public CacheWarmupHostedService(
        IServiceProvider services,
        IOptions<CacheOptions> options,
        ILogger<CacheWarmupHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Exposed so a test or an operational endpoint can warm on demand without a host restart.
    /// <para>
    /// The same shape the reference-data seeder established: a public method that swallows, called
    /// by the hosted service. A warmer that could only run at startup could only be tested at startup.
    /// </para>
    /// </summary>
    public async Task<int> WarmAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();

        var warmers = scope.ServiceProvider.GetServices<ICacheWarmer>().ToArray();

        if (warmers.Length == 0)
        {
            return 0;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.WarmupTimeout);

        var total = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var warmer in warmers)
        {
            if (timeout.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Cache warming ran out of time after {Elapsed}ms. {Remaining} warmer(s) did not run; " +
                    "the instance is serving cold for those families.",
                    stopwatch.ElapsedMilliseconds,
                    warmers.Length - warmers.TakeWhile(candidate => candidate != warmer).Count());

                break;
            }

            try
            {
                var written = await warmer.WarmAsync(timeout.Token).ConfigureAwait(false);
                total += written;

                OperationsTelemetry.CacheWarmedEntries.Add(written,
                    new KeyValuePair<string, object?>(
                        PlatformTelemetryNames.Attributes.Module, warmer.Module),
                    new KeyValuePair<string, object?>(
                        PlatformTelemetryNames.Attributes.CacheNamespace, warmer.CacheNamespace));

                _logger.LogInformation(
                    "Warmed {Count} entrie(s) into {CacheNamespace} for {Module}.",
                    written, warmer.CacheNamespace, warmer.Module);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Cache warmer {Warmer} for {CacheNamespace} failed. The family stays cold.",
                    warmer.GetType().Name, warmer.CacheNamespace);
            }
        }

        _logger.LogInformation(
            "Cache warming finished: {Total} entrie(s) across {Warmers} warmer(s) in {Elapsed}ms.",
            total, warmers.Length, stopwatch.ElapsedMilliseconds);

        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.WarmOnStartup)
        {
            return;
        }

        try
        {
            await WarmAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down before warming finished. Nothing to report.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Cache warming failed. The instance is serving cold.");
        }
    }
}
