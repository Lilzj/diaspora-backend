using System.Diagnostics;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Operations.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Workers;

/// <summary>
/// The standard background worker.
/// <para>
/// Every loop the platform runs should be one of these. Before B17 each was a bespoke
/// <c>BackgroundService</c> with its own <c>while (!stoppingToken.IsCancellationRequested)</c>, its
/// own delay, its own try/catch and — in several cases — no catch at all, which in a
/// <c>BackgroundService</c> means the loop dies silently and the host keeps running as though
/// nothing happened. That failure has no log line, no metric and no health signal; the first
/// symptom is that something downstream stopped changing.
/// </para>
/// <para>
/// What a subclass gets by inheriting: a loop with cancellation handled correctly, an exception
/// boundary, escalating backoff on consecutive failures, lifecycle and health reporting, metrics
/// labelled by module and worker, a span per iteration, and a queue policy it can read its own
/// batch size and concurrency from. What it has to write is <see cref="RunOnceAsync"/>.
/// </para>
/// <para>
/// <b>An iteration that throws never stops the loop.</b> It backs off — up to
/// <see cref="MaxBackoff"/> — and keeps trying, because a transient failure is the common case and a
/// worker that exits on the first one needs a process restart to recover. A worker that has failed
/// <see cref="FaultThreshold"/> times consecutively reports <see cref="WorkerLifecycle.Recovering"/>
/// so the health check and the dashboard can say so.
/// </para>
/// </summary>
public abstract class PlatformWorker : BackgroundService
{
    /// <summary>Consecutive failures after which the worker reports itself as recovering.</summary>
    protected const int FaultThreshold = 3;

    /// <summary>Ceiling on the failure backoff.</summary>
    protected static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly IOperationalPolicyCatalog _policies;

    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _lastRunAt;
    private DateTimeOffset? _lastSuccessAt;
    private TimeSpan _lastRunDuration;
    private int _consecutiveFailures;
    private long _totalRuns;
    private long _itemsProcessed;
    private string? _lastError;
    private WorkerLifecycle _lifecycle = WorkerLifecycle.Created;

    protected PlatformWorker(
        string name,
        string module,
        IOperationalPolicyCatalog policies,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(logger);

        Name = name;
        Module = module;
        _policies = policies;
        Logger = logger;
    }

    /// <summary>Unique within the process. Appears in every log line, metric and health entry.</summary>
    public string Name { get; }

    /// <summary>The owning module.</summary>
    public string Module { get; }

    protected ILogger Logger { get; }

    /// <summary>
    /// The registry this worker reports to. Set by the host after construction rather than injected,
    /// because a worker constructed in a unit test has no registry and should still run.
    /// </summary>
    public IWorkerRegistry? Registry { get; set; }

    /// <summary>How long to wait between iterations when the last one found nothing to do.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// The queue policy this worker reads its batch size, concurrency and thresholds from. Override
    /// to name a specific one; the default is the platform's.
    /// </summary>
    protected virtual string QueuePolicyName => OperationalPolicyNames.Queue.Default;

    /// <summary>
    /// Whether the worker runs on this host at all. Set from configuration by
    /// <see cref="PlatformWorkerRegistration.AddPlatformWorker{TWorker}"/>.
    /// <para>
    /// <b>The switch DEBT-049 recorded as missing on four modules.</b> An integration harness that
    /// shares one SQLite connection cannot tolerate a background loop querying it mid-request, and
    /// four sweepers had no way to be turned off at all — so closing that debt means adding a switch
    /// to each of them. Every <see cref="PlatformWorker"/> has one from the start, which is the half
    /// of the problem a platform can actually fix: the shape cannot be recreated by a new worker.
    /// </para>
    /// <para>
    /// It is also the mechanism behind DEBT-015's mitigation — "disable sweepers on all but one
    /// instance" — until a real distributed lock is registered.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The resolved queue policy. Read once per iteration so a hot-reloaded change takes effect.</summary>
    protected QueuePolicy Policy => _policies.Get(QueuePolicyName, DefaultQueuePolicy);

    /// <summary>
    /// The policy catalogue, for a worker that needs more than its queue policy.
    /// <para>
    /// Exposed rather than re-injected by each subclass, which is how two copies of one catalogue end
    /// up in one worker and only one of them gets the hot-reloaded values.
    /// </para>
    /// </summary>
    protected IOperationalPolicyCatalog Policies => _policies;

    /// <summary>
    /// One iteration.
    /// </summary>
    /// <returns>
    /// How many items were processed. Zero means idle and the loop waits <see cref="Interval"/>; a
    /// full batch means there is more to do and the loop runs again immediately, which is what lets a
    /// worker drain a backlog without waiting an interval per batch.
    /// </returns>
    protected abstract Task<int> RunOnceAsync(CancellationToken cancellationToken);

    /// <summary>This worker's current status.</summary>
    public WorkerStatus Status => new(
        Name,
        Module,
        _lifecycle,
        _startedAt,
        _lastRunAt,
        _lastSuccessAt,
        _lastRunDuration,
        _consecutiveFailures,
        _totalRuns,
        _itemsProcessed,
        _lastError,
        Policy.Concurrency);

    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            Logger.LogInformation("Worker {Worker} is disabled on this host and will not start.", Name);
            return;
        }

        _startedAt = DateTimeOffset.UtcNow;
        Transition(WorkerLifecycle.Running);

        Logger.LogInformation("Worker {Worker} started for {Module}.", Name, Module);

        // Yields immediately so the host finishes starting. A BackgroundService whose ExecuteAsync
        // runs synchronously to its first await blocks every service registered after it, which is
        // how one slow worker delays a whole host's startup.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await IterateAsync(stoppingToken).ConfigureAwait(false);

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var delay = NextDelay(processed);

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        Transition(_consecutiveFailures >= FaultThreshold ? WorkerLifecycle.Faulted : WorkerLifecycle.Stopped);

        Logger.LogInformation(
            "Worker {Worker} stopped after {Runs} run(s) and {Items} item(s).",
            Name, _totalRuns, _itemsProcessed);
    }

    private async Task<int> IterateAsync(CancellationToken cancellationToken)
    {
        using var activity = OperationsTelemetry.Source.StartActivity(
            $"worker {Name}", ActivityKind.Internal);

        activity?.SetTag(PlatformTelemetryNames.Attributes.Module, Module);
        activity?.SetTag(PlatformTelemetryNames.Attributes.Operation, Name);

        _lastRunAt = DateTimeOffset.UtcNow;
        _totalRuns++;

        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";
        var processed = 0;

        try
        {
            processed = await RunOnceAsync(cancellationToken).ConfigureAwait(false);

            _itemsProcessed += processed;
            _consecutiveFailures = 0;
            _lastError = null;
            _lastSuccessAt = DateTimeOffset.UtcNow;

            if (_lifecycle == WorkerLifecycle.Recovering)
            {
                Logger.LogInformation("Worker {Worker} recovered.", Name);
                Transition(WorkerLifecycle.Running);
            }

            activity?.SetTag("diaspora.worker.items", processed);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            activity?.SetStatus(ActivityStatusCode.Unset);
        }
        catch (Exception exception)
        {
            outcome = "failure";
            _consecutiveFailures++;
            _lastError = exception.Message;

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity?.AddException(exception);

            OperationsTelemetry.WorkerFailures.Add(1,
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Module, Module),
                new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, Name));

            if (_consecutiveFailures >= FaultThreshold)
            {
                Transition(WorkerLifecycle.Recovering);

                Logger.LogError(
                    exception,
                    "Worker {Worker} has failed {Count} time(s) in a row and is backing off.",
                    Name, _consecutiveFailures);
            }
            else
            {
                Logger.LogWarning(exception, "Worker {Worker} iteration failed.", Name);
            }
        }
        finally
        {
            stopwatch.Stop();
            _lastRunDuration = stopwatch.Elapsed;

            var tags = new TagList
            {
                { PlatformTelemetryNames.Attributes.Module, Module },
                { PlatformTelemetryNames.Attributes.Operation, Name },
                { PlatformTelemetryNames.Attributes.Outcome, outcome }
            };

            OperationsTelemetry.WorkerRuns.Add(1, tags);
            OperationsTelemetry.WorkerRunDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

            Registry?.Report(Status);
        }

        return processed;
    }

    /// <summary>
    /// How long to wait before the next iteration.
    /// <para>
    /// Three cases and they are all load-bearing. A failing worker backs off exponentially, capped,
    /// so a dependency outage does not become a tight retry loop against it. A worker that filled its
    /// batch does not wait at all, so a backlog drains at the rate the work allows rather than at one
    /// batch per interval. Everything else waits the interval.
    /// </para>
    /// </summary>
    private TimeSpan NextDelay(int processed)
    {
        if (_consecutiveFailures > 0)
        {
            var backoff = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(_consecutiveFailures, 8)));
            return backoff > MaxBackoff ? MaxBackoff : backoff;
        }

        return processed >= Policy.BatchSize ? TimeSpan.Zero : Interval;
    }

    private void Transition(WorkerLifecycle lifecycle)
    {
        _lifecycle = lifecycle;
        Registry?.Report(Status);
    }

    private static readonly QueuePolicy DefaultQueuePolicy =
        new(OperationalPolicyNames.Queue.Default)
        {
            Description = "Compiled-in fallback, used when the catalogue has no queue policy."
        };
}

/// <summary>
/// Wires a <see cref="PlatformWorker"/> into the host with its registry attached.
/// </summary>
public static class PlatformWorkerRegistration
{
    /// <summary>
    /// Registers a worker as a hosted service and as itself, with the registry injected after
    /// construction.
    /// <para>
    /// Registered under both so an operational endpoint can resolve the concrete worker to read its
    /// status or rewind it, which resolving <c>IHostedService</c> cannot do. The single instance is
    /// shared: two instances of one worker would run the same loop twice.
    /// </para>
    /// </summary>
    /// <summary>The configuration section every worker's enable switch lives under.</summary>
    public const string ConfigurationSection = "Operations:Workers";

    public static IServiceCollection AddPlatformWorker<TWorker>(this IServiceCollection services)
        where TWorker : PlatformWorker
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TWorker>(provider =>
        {
            var worker = ActivatorUtilities.CreateInstance<TWorker>(provider);
            worker.Registry = provider.GetService<IWorkerRegistry>();

            // Two switches, per-worker beating the global one. A deployment running sweepers on a
            // single instance turns the global switch off everywhere and back on for one host, which
            // is the mitigation DEBT-015 records; a deployment chasing one misbehaving loop turns off
            // just that one.
            var configuration = provider.GetService<IConfiguration>();

            if (configuration is not null)
            {
                var global = configuration.GetValue($"{ConfigurationSection}:Enabled", defaultValue: true);

                worker.Enabled = configuration.GetValue(
                    $"{ConfigurationSection}:{worker.Name}:Enabled", defaultValue: global);
            }

            return worker;
        });

        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<TWorker>());

        return services;
    }
}
