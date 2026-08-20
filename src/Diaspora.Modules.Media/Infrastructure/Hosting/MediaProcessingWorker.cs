using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Modules.Media.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Infrastructure.Hosting;

/// <summary>
/// Runs the processing jobs that did not finish inline.
/// <para>
/// Two populations reach it. Jobs a deployment configured to run out-of-band — which is what
/// any deployment with video transcoding must do, because a transcode is minutes and no
/// request should hold a connection that long. And jobs whose inline attempt exceeded its
/// budget, which is the escape hatch that makes inline processing safe for the pathological
/// file that takes thirty seconds to decode.
/// </para>
/// <para>
/// Deliberately a database-polled queue rather than a message broker. The platform has no
/// broker yet — <c>IBackgroundJobScheduler</c> is still declared-not-implemented — and a
/// module that required one would not run. The seam is the abstraction, not the transport: a
/// later phase that introduces a broker replaces this hosted service and nothing else changes.
/// </para>
/// </summary>
public sealed class MediaProcessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly MediaProcessingOptions _options;
    private readonly ILogger<MediaProcessingWorker> _logger;

    public MediaProcessingWorker(
        IServiceScopeFactory scopes,
        IOptions<MediaProcessingOptions> options,
        ILogger<MediaProcessingWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableWorker)
        {
            _logger.LogInformation("MediaProcessingWorker is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerIntervalSeconds));

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception, "The media processing worker failed; it will run again.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One batch. Public so a test can drive it without waiting for a tick.</summary>
    public async Task RunBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();

        var jobs = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
        var assets = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var pipeline = scope.ServiceProvider.GetRequiredService<MediaProcessingPipeline>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var batch = Math.Max(1, _options.WorkerBatchSize);

        var pending = await jobs.FindPendingAsync(batch, cancellationToken);
        var retryable = await jobs.FindRetryableAsync(batch, cancellationToken);

        foreach (var job in pending.Concat(retryable))
        {
            var asset = await assets.GetAsync(job.MediaId, cancellationToken);

            if (asset is null)
            {
                // The asset was deleted between queueing and running. Cancelling the job is
                // the honest record; leaving it pending would make it a permanent resident of
                // every future batch.
                job.Cancel(DateTimeOffset.UtcNow);
                continue;
            }

            if (job.Status is Domain.Enums.ProcessingJobStatus.Failed)
            {
                job.Requeue();
            }

            await pipeline.ExecuteAsync(job, asset, cancellationToken);
        }

        if (pending.Count + retryable.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Processed {Pending} pending and {Retry} retryable media job(s).",
                pending.Count, retryable.Count);
        }
    }
}
