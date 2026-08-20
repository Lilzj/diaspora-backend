using System.Diagnostics;
using Diaspora.Operations.Telemetry;
using Diaspora.Operations.Workers;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Operations.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Scheduling;

/// <summary>
/// Runs scheduled jobs.
/// <para>
/// Promotes due recurring registrations, then drains the pending queue until it is empty or the batch
/// is full. Each job runs in its own DI scope, because a handler that touches the database needs a
/// scoped context and one shared across a whole batch would accumulate every entity the batch loaded.
/// </para>
/// <para>
/// <b>Every job runs under a span linked to the correlation id it was scheduled with.</b> Without
/// that, a background job is an orphan in the tracing backend and "what caused this write" has no
/// answer — which is the single most useful thing tracing gives an operator investigating a job.
/// </para>
/// </summary>
public sealed class JobExecutionWorker : PlatformWorker
{
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(6);

    private readonly InMemoryJobScheduler _scheduler;
    private readonly IServiceProvider _services;

    private DateTimeOffset _lastPrunedAt = DateTimeOffset.MinValue;

    public JobExecutionWorker(
        InMemoryJobScheduler scheduler,
        IServiceProvider services,
        IOperationalPolicyCatalog policies,
        ILogger<JobExecutionWorker> logger)
        : base("job-execution", "Platform", policies, logger)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(services);

        _scheduler = scheduler;
        _services = services;
    }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(1);

    protected override string QueuePolicyName => OperationalPolicyNames.Queue.Default;

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        _scheduler.PromoteDueRecurring();

        var policy = Policy;
        var retry = Policies.Get(OperationalPolicyNames.Retry.Default, FallbackRetry);
        var processed = 0;

        while (processed < policy.BatchSize && !cancellationToken.IsCancellationRequested)
        {
            var job = _scheduler.Claim();

            if (job is null)
            {
                break;
            }

            await RunAsync(job, retry.PoisonThreshold, cancellationToken).ConfigureAwait(false);
            processed++;
        }

        Prune();

        return processed;
    }

    private async Task RunAsync(
        InMemoryJobScheduler.JobRecord job, int poisonThreshold, CancellationToken cancellationToken)
    {
        using var activity = OperationsTelemetry.Source.StartActivity(
            $"job {job.JobType}", ActivityKind.Consumer);

        activity?.SetTag(PlatformTelemetryNames.Attributes.Operation, job.JobType);
        activity?.SetTag(PlatformTelemetryNames.Attributes.Module, Module);

        if (job.CorrelationId is { } correlationId)
        {
            activity?.SetTag(PlatformTelemetryNames.Attributes.CorrelationId, correlationId);
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, job.Cancellation.Token);

        using var scope = _services.CreateScope();

        var handler = scope.ServiceProvider
            .GetServices<IJobHandler>()
            .LastOrDefault(candidate => string.Equals(candidate.JobType, job.JobType, StringComparison.Ordinal));

        if (handler is null)
        {
            // A job with no handler is a permanent failure, not a retryable one. Retrying it until it
            // dead-letters would take the poison threshold's worth of attempts to reach the same
            // answer, and would look like a failing dependency in every metric on the way.
            var missing = new InvalidOperationException(
                $"No IJobHandler is registered for job type '{job.JobType}'.");

            Logger.LogError(missing, "Job {JobId} cannot run.", job.Id);
            _scheduler.Complete(job, missing, poisonThreshold: 1);

            activity?.SetStatus(ActivityStatusCode.Error, missing.Message);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await handler.ExecuteAsync(
                new JobContext(job.Id, job.JobType, job.Payload, job.Attempts, job.CorrelationId),
                linked.Token).ConfigureAwait(false);

            _scheduler.Complete(job, failure: null, poisonThreshold);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
        {
            Logger.LogInformation("Job {JobId} ({JobType}) was cancelled.", job.Id, job.JobType);
            activity?.SetStatus(ActivityStatusCode.Unset);
        }
        catch (Exception exception)
        {
            _scheduler.Complete(job, exception, poisonThreshold);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);

            Logger.LogWarning(
                exception,
                "Job {JobId} ({JobType}) failed on attempt {Attempt}.",
                job.Id, job.JobType, job.Attempts);
        }
        finally
        {
            stopwatch.Stop();

            OperationsTelemetry.WorkerRunDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Module, Module),
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, job.JobType));
        }
    }

    /// <summary>
    /// Discards finished jobs, at most once an hour.
    /// <para>
    /// Rate-limited rather than run every second: the sweep walks the whole dictionary, and doing that
    /// on a worker whose interval is one second would spend more time pruning than working.
    /// </para>
    /// </summary>
    private void Prune()
    {
        var now = DateTimeOffset.UtcNow;

        if (now - _lastPrunedAt < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastPrunedAt = now;

        var removed = _scheduler.Prune(CompletedJobRetention);

        if (removed > 0)
        {
            Logger.LogDebug("Pruned {Count} finished job record(s).", removed);
        }
    }

    private static readonly RetryPolicy FallbackRetry = new(OperationalPolicyNames.Retry.Default);
}
