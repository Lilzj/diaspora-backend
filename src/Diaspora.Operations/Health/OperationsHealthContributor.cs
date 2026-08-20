using System.Globalization;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Search;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Workers;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Operations.Health;

/// <summary>
/// The Production Platform's own health: cache, workers, breakers, secrets and search.
/// <para>
/// Every one of these degrades and none is critical, which is deliberate and is the health policy's
/// whole argument. A cache that is down, a worker that has stalled or a breaker that is open are all
/// reasons to look at an instance and none of them is a reason to stop sending it traffic — removing
/// it would concentrate the same load on the replicas that have the same problem.
/// </para>
/// </summary>
public sealed class OperationsHealthContributor : IModuleHealthContributor
{
    /// <summary>How far ahead the secret check looks for an expiry.</summary>
    private static readonly TimeSpan SecretExpiryWindow = TimeSpan.FromDays(14);

    /// <summary>How long a worker may go without a successful run before it is reported as stalled.</summary>
    private static readonly TimeSpan WorkerStallThreshold = TimeSpan.FromHours(1);

    private readonly ICacheStatisticsProvider _cache;
    private readonly IWorkerRegistry _workers;
    private readonly ICircuitBreakerRegistry _breakers;
    private readonly ISearchProvider _search;
    private readonly ISecretStore _secrets;
    private readonly IClock _clock;

    public OperationsHealthContributor(
        ICacheStatisticsProvider cache,
        IWorkerRegistry workers,
        ICircuitBreakerRegistry breakers,
        ISearchProvider search,
        ISecretStore secrets,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(workers);
        ArgumentNullException.ThrowIfNull(breakers);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(clock);

        _cache = cache;
        _workers = workers;
        _breakers = breakers;
        _search = search;
        _secrets = secrets;
        _clock = clock;
    }

    public string Module => "Platform";

    public async Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        return
        [
            CheckCache(),
            CheckWorkers(),
            CheckBreakers(),
            await CheckSearchAsync(cancellationToken).ConfigureAwait(false),
            await CheckSecretsAsync(cancellationToken).ConfigureAwait(false)
        ];
    }

    private ModuleHealthResult CheckCache()
    {
        var stats = _cache.Snapshot();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["localProvider"] = stats.LocalProvider,
            ["distributedProvider"] = stats.DistributedProvider ?? "(none)",
            ["hitRatio"] = Math.Round(stats.HitRatio, 3),
            ["hits"] = stats.Hits,
            ["misses"] = stats.Misses,
            ["errors"] = stats.Errors,
            ["namespaces"] = stats.Namespaces.Count
        };

        if (stats.Errors > 0)
        {
            return ModuleHealthResult.Degraded(
                "platform.cache", Module, HealthDimension.Cache,
                "The cache store is returning errors. Reads are falling through to their sources.",
                data);
        }

        if (!stats.IsDistributed)
        {
            // Not an error, and worth reporting on every probe. A multi-level policy served entirely
            // from process memory is inconsistent across replicas and an invalidation reaches only the
            // replica that performed it — a correctness property an operator should not have to infer.
            return ModuleHealthResult.Degraded(
                "platform.cache", Module, HealthDimension.Cache,
                "No shared cache store is registered. Distributed and multi-level policies are served " +
                "from process memory, so entries differ between replicas.",
                data);
        }

        return ModuleHealthResult.Healthy(
            "platform.cache", Module, HealthDimension.Cache, "The cache is answering.", data);
    }

    private ModuleHealthResult CheckWorkers()
    {
        var workers = _workers.All;
        var now = _clock.UtcNow;

        var stalled = workers
            .Where(worker =>
                worker.Lifecycle is WorkerLifecycle.Running or WorkerLifecycle.Recovering &&
                (worker.LastSuccessAt is null
                    ? worker.StartedAt is { } started && now - started > WorkerStallThreshold
                    : now - worker.LastSuccessAt.Value > WorkerStallThreshold))
            .ToArray();

        var faulted = workers.Where(worker => worker.Lifecycle == WorkerLifecycle.Faulted).ToArray();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["running"] = workers.Count(worker => worker.Lifecycle == WorkerLifecycle.Running),
            ["recovering"] = workers.Count(worker => worker.Lifecycle == WorkerLifecycle.Recovering),
            ["faulted"] = faulted.Length,
            ["stalled"] = stalled.Select(worker => worker.Name).ToArray()
        };

        if (faulted.Length > 0 || stalled.Length > 0)
        {
            return ModuleHealthResult.Degraded(
                "platform.workers", Module, HealthDimension.BackgroundWorker,
                $"{faulted.Length + stalled.Length} background worker(s) are not making progress.",
                data);
        }

        return ModuleHealthResult.Healthy(
            "platform.workers", Module, HealthDimension.BackgroundWorker,
            $"{workers.Count} worker(s) running.", data);
    }

    private ModuleHealthResult CheckBreakers()
    {
        var open = _breakers.All
            .Where(breaker => breaker.State is CircuitState.Open or CircuitState.Isolated)
            .ToArray();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["tracked"] = _breakers.All.Count,
            ["open"] = open.Select(breaker => breaker.Dependency).ToArray()
        };

        if (open.Length > 0)
        {
            return ModuleHealthResult.Degraded(
                "platform.circuits", Module, HealthDimension.ExternalProvider,
                $"{open.Length} circuit(s) are refusing calls: {string.Join(", ", open.Select(breaker => breaker.Dependency))}.",
                data);
        }

        return ModuleHealthResult.Healthy(
            "platform.circuits", Module, HealthDimension.ExternalProvider,
            "Every circuit is closed.", data);
    }

    private async Task<ModuleHealthResult> CheckSearchAsync(CancellationToken cancellationToken)
    {
        var available = await _search.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["provider"] = _search.Name,
            ["available"] = available
        };

        // Healthy, not degraded. No search provider is the platform's shipped and intended state —
        // every search port falls back to a database-backed engine — and reporting it as degraded would
        // put every instance on the platform in a permanently amber state, which trains people to
        // ignore the colour.
        return available
            ? ModuleHealthResult.Healthy(
                "platform.search", Module, HealthDimension.Search,
                $"The {_search.Name} search provider is answering.", data)
            : ModuleHealthResult.Healthy(
                "platform.search", Module, HealthDimension.Search,
                "No search provider is registered. Each module is serving search from its own " +
                "database-backed engine, which is the shipped configuration.", data);
    }

    private async Task<ModuleHealthResult> CheckSecretsAsync(CancellationToken cancellationToken)
    {
        var problems = await _secrets.ValidateAsync(cancellationToken).ConfigureAwait(false);
        var descriptors = await _secrets.DescribeAllAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;

        var expiring = descriptors
            .Where(descriptor => descriptor.IsExpiringSoon(now, SecretExpiryWindow))
            .Select(descriptor => descriptor.Name)
            .ToArray();

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["declared"] = descriptors.Count,
            ["problems"] = problems.Count,
            ["expiringSoon"] = expiring
        };

        if (problems.Count > 0)
        {
            // The count and the names, never a value. A health endpoint is reachable from more places
            // inside a cluster than it should be.
            return ModuleHealthResult.Degraded(
                "platform.secrets", Module, HealthDimension.Dependency,
                $"{problems.Count.ToString(CultureInfo.InvariantCulture)} secret(s) failed validation.",
                data);
        }

        if (expiring.Length > 0)
        {
            return ModuleHealthResult.Degraded(
                "platform.secrets", Module, HealthDimension.Dependency,
                $"{expiring.Length} secret(s) are due for rotation within {SecretExpiryWindow.TotalDays} days.",
                data);
        }

        return ModuleHealthResult.Healthy(
            "platform.secrets", Module, HealthDimension.Dependency,
            "Every declared secret is present and unexpired.", data);
    }
}
