using System.Collections.Concurrent;
using System.Diagnostics;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Telemetry;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Health;

/// <summary>
/// Runs every module's health contributor and folds the answers into one model.
/// <para>
/// <b>Contributors run concurrently and each under its own timeout.</b> Serially, fifteen modules each
/// taking a second would produce a readiness probe that times out at the orchestrator before it
/// finishes — and when a probe times out, the reason is lost and every check is reported as unknown.
/// </para>
/// <para>
/// <b>Results are cached per the health policy.</b> Probes arrive every few seconds from the
/// orchestrator, the load balancer and the dashboard; a dependency check that opens a real connection
/// must not run once per probe per source. The cache duration is the policy's, so a cheap check can
/// have none.
/// </para>
/// <para>
/// <b>The aggregate is not simply the worst result.</b> A non-critical check that is unhealthy
/// degrades the platform; only a check whose policy says <c>Critical</c> can make the aggregate
/// unhealthy. That is what stops a third-party outage taking every instance out of rotation, and it is
/// the reason <c>Critical</c> is false by default.
/// </para>
/// </summary>
public sealed class PlatformHealthAggregator : IPlatformHealthAggregator
{
    private static readonly HealthPolicy FallbackPolicy = new(OperationalPolicyNames.Health.Default);

    private readonly ConcurrentDictionary<string, CachedResults> _cache = new(StringComparer.Ordinal);
    private readonly IServiceScopeFactory _scopes;
    private readonly IOperationalPolicyCatalog _policies;
    private readonly IClock _clock;
    private readonly ILogger<PlatformHealthAggregator> _logger;
    private readonly string _instance;

    /// <summary>
    /// <paramref name="scopes"/> rather than the contributors themselves, and that is not fastidiousness.
    /// <para>
    /// A module's health contributor almost always needs a <c>DbContext</c> — checking a dependency
    /// usually means asking it something — which makes the contributor scoped. This aggregator is a
    /// singleton, because it caches results across probes. A singleton that captured a scoped
    /// contributor would resolve it once at startup and hold that instance's <c>DbContext</c> for the
    /// life of the process, which the container refuses to build and would otherwise be a disposed
    /// context on the second probe.
    /// </para>
    /// </summary>
    public PlatformHealthAggregator(
        IServiceScopeFactory scopes,
        IOperationalPolicyCatalog policies,
        IClock clock,
        ILogger<PlatformHealthAggregator> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _scopes = scopes;
        _policies = policies;
        _clock = clock;
        _logger = logger;
        _instance = $"{Environment.MachineName}/{Environment.ProcessId}";
    }

    public async Task<PlatformHealthModel> CheckAsync(
        HealthDimension? dimension = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // One scope for the whole pass, disposed when it finishes. Not one per contributor: a health
        // pass is a single logical operation, and fifteen scopes means fifteen database connections
        // taken from the pool at once every time an orchestrator probes.
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider.GetServices<IModuleHealthContributor>().ToArray();

        var passes = await Task.WhenAll(
            contributors.Select(contributor => RunAsync(contributor, cancellationToken)))
            .ConfigureAwait(false);

        var results = passes
            .SelectMany(pass => pass)
            .Where(result => dimension is null || result.Dimension == dimension)
            .OrderBy(result => result.Module, StringComparer.Ordinal)
            .ThenBy(result => result.Name, StringComparer.Ordinal)
            .ToArray();

        stopwatch.Stop();

        var byDimension = results
            .GroupBy(result => result.Dimension)
            .ToDictionary(group => group.Key, group => group.Max(result => result.State));

        return new PlatformHealthModel(
            Aggregate(results),
            _instance,
            _clock.UtcNow,
            stopwatch.Elapsed,
            results,
            byDimension);
    }

    /// <summary>
    /// The platform verdict.
    /// <para>
    /// Unhealthy only when a <em>critical</em> check is unhealthy. Everything else — a non-critical
    /// check failing, any check degrading — degrades. The distinction is the whole point of the
    /// health policy: without it, the first third-party provider to have a bad afternoon takes the
    /// entire deployment out of rotation.
    /// </para>
    /// </summary>
    private PlatformHealthState Aggregate(IReadOnlyList<ModuleHealthResult> results)
    {
        var worst = PlatformHealthState.Healthy;

        foreach (var result in results)
        {
            if (result.State == PlatformHealthState.Healthy)
            {
                continue;
            }

            if (result.State == PlatformHealthState.Unhealthy && PolicyFor(result).Critical)
            {
                return PlatformHealthState.Unhealthy;
            }

            worst = PlatformHealthState.Degraded;
        }

        return worst;
    }

    private async Task<IReadOnlyList<ModuleHealthResult>> RunAsync(
        IModuleHealthContributor contributor, CancellationToken cancellationToken)
    {
        var policy = _policies.Get(OperationalPolicyNames.Health.Default, FallbackPolicy);
        var now = _clock.UtcNow;

        if (_cache.TryGetValue(contributor.Module, out var cached) &&
            now - cached.At < policy.CacheDuration)
        {
            return cached.Results;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(policy.Timeout);

            var results = await contributor.CheckAsync(timeout.Token).ConfigureAwait(false);

            stopwatch.Stop();

            // Stamped here rather than trusted from the contributor. A contributor that forgot to
            // time itself would report zero, and a zero duration on a check that is timing out is the
            // most misleading number the endpoint could show.
            var stamped = results
                .Select(result => result with { Duration = stopwatch.Elapsed })
                .ToArray();

            Record(stamped, stopwatch.Elapsed);
            _cache[contributor.Module] = new CachedResults(stamped, now);

            return stamped;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            var timedOut = new[]
            {
                ModuleHealthResult.Unhealthy(
                    $"{contributor.Module}.health",
                    contributor.Module,
                    HealthDimension.Dependency,
                    $"The module's health check exceeded its {policy.Timeout} timeout.") with
                {
                    Duration = stopwatch.Elapsed
                }
            };

            Record(timedOut, stopwatch.Elapsed);

            // Not cached. A timeout is exactly the condition where the next probe should try again
            // rather than be told the same stale answer for the cache duration.
            return timedOut;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            stopwatch.Stop();

            _logger.LogError(
                exception, "The health contributor for {Module} threw.", contributor.Module);

            var failed = new[]
            {
                ModuleHealthResult.Unhealthy(
                    $"{contributor.Module}.health",
                    contributor.Module,
                    HealthDimension.Dependency,
                    "The module's health check threw.",
                    error: exception.Message) with
                {
                    Duration = stopwatch.Elapsed
                }
            };

            Record(failed, stopwatch.Elapsed);

            return failed;
        }
    }

    private HealthPolicy PolicyFor(ModuleHealthResult result)
    {
        var named = result.Dimension switch
        {
            HealthDimension.Storage => OperationalPolicyNames.Health.Storage,
            HealthDimension.Messaging => OperationalPolicyNames.Health.Messaging,
            HealthDimension.Cache => OperationalPolicyNames.Health.Cache,
            HealthDimension.Search => OperationalPolicyNames.Health.Search,
            HealthDimension.ExternalProvider => OperationalPolicyNames.Health.ExternalProvider,
            HealthDimension.BackgroundWorker => OperationalPolicyNames.Health.BackgroundWorker,
            _ => OperationalPolicyNames.Health.Default
        };

        return _policies.Find<HealthPolicy>(named)
            ?? _policies.Find<HealthPolicy>(OperationalPolicyNames.Health.Default)
            ?? FallbackPolicy;
    }

    private static void Record(IReadOnlyList<ModuleHealthResult> results, TimeSpan elapsed)
    {
        foreach (var result in results)
        {
            var tags = new TagList
            {
                { PlatformTelemetryNames.Attributes.Module, result.Module },
                { PlatformTelemetryNames.Attributes.HealthDimension, result.Dimension.ToString() },
                { PlatformTelemetryNames.Attributes.Outcome, result.State.ToString() }
            };

            OperationsTelemetry.HealthChecks.Add(1, tags);
            OperationsTelemetry.HealthCheckDuration.Record(elapsed.TotalMilliseconds, tags);
        }
    }

    private sealed record CachedResults(IReadOnlyList<ModuleHealthResult> Results, DateTimeOffset At);
}
