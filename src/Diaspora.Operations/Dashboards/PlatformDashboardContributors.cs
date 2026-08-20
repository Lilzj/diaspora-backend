using System.Globalization;
using Diaspora.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Dashboards;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Recovery;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Scalability;
using Diaspora.Platform.Operations.Workers;

namespace Diaspora.Operations.Dashboards;

/// <summary>
/// The platform-health board: the aggregate verdict, every check, the topology, and what is not
/// configured.
/// <para>
/// The last panel is the one that matters most and is the one such boards usually omit. A board that
/// only shows what is running cannot tell an operator that there is no backup provider, no shared
/// cache and no search engine — three facts that change what every other number on the board means.
/// </para>
/// </summary>
public sealed class PlatformHealthDashboardContributor : IDashboardContributor
{
    private readonly IPlatformHealthAggregator _health;
    private readonly IRegionContext _region;
    private readonly IBackupProvider _backups;
    private readonly ICacheStatisticsProvider _cache;

    public PlatformHealthDashboardContributor(
        IPlatformHealthAggregator health,
        IRegionContext region,
        IBackupProvider backups,
        ICacheStatisticsProvider cache)
    {
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(cache);

        _health = health;
        _region = region;
        _backups = backups;
        _cache = cache;
    }

    public string Dashboard => DashboardNames.PlatformHealth;

    public string Module => "Platform";

    public async Task<IReadOnlyList<DashboardPanel>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var model = await _health.CheckAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var latestBackup = await _backups.LatestAsync(cancellationToken).ConfigureAwait(false);
        var cache = _cache.Snapshot();

        return
        [
            new DashboardPanel(
                "Aggregate",
                [
                    new DashboardMetric("State", model.State.ToString(), StatusOf(model.State)),
                    new DashboardMetric("Instance", model.Instance),
                    new DashboardMetric(
                        "Check duration",
                        model.TotalDuration.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture),
                        DashboardStatus.Neutral, "ms"),
                    new DashboardMetric(
                        "Checks",
                        model.Results.Count.ToString(CultureInfo.InvariantCulture))
                ],
                Module,
                "Health is per-instance. An aggregate across replicas is the dashboard's job, not this one's."),

            new DashboardPanel(
                "By dimension",
                [
                    .. model.ByDimension
                        .OrderBy(entry => entry.Key)
                        .Select(entry => new DashboardMetric(
                            entry.Key.ToString(), entry.Value.ToString(), StatusOf(entry.Value)))
                ],
                Module),

            new DashboardPanel(
                "Failing checks",
                !model.Results.Any(result => result.State != PlatformHealthState.Healthy)
                    ? [new DashboardMetric("All checks", "healthy", DashboardStatus.Good)]
                    :
                    [
                        .. model.Results
                            .Where(result => result.State != PlatformHealthState.Healthy)
                            .Select(result => new DashboardMetric(
                                $"{result.Module}/{result.Name}",
                                result.State.ToString(),
                                StatusOf(result.State),
                                Hint: result.Description))
                    ],
                Module),

            new DashboardPanel(
                "Topology",
                [
                    new DashboardMetric("Region", _region.CurrentRegion),
                    new DashboardMetric("Zone", _region.CurrentZone ?? "(unknown)",
                        _region.CurrentZone is null ? DashboardStatus.Unknown : DashboardStatus.Neutral),
                    new DashboardMetric("Role", _region.IsPrimary ? "primary" : "secondary"),
                    new DashboardMetric(
                        "Configured regions",
                        _region.Regions.Count.ToString(CultureInfo.InvariantCulture))
                ],
                Module),

            new DashboardPanel(
                "Not configured",
                [
                    new DashboardMetric(
                        "Shared cache",
                        cache.DistributedProvider ?? "none",
                        cache.IsDistributed ? DashboardStatus.Good : DashboardStatus.Unknown,
                        Hint: cache.IsDistributed
                            ? null
                            : "Multi-level policies are served from process memory. Entries differ " +
                              "between replicas and an invalidation reaches only one of them."),

                    new DashboardMetric(
                        "Backup provider",
                        _backups.Name,
                        latestBackup is null ? DashboardStatus.Unknown : DashboardStatus.Good,
                        Hint: latestBackup is null
                            ? "Nothing reports backups to the platform. The recovery objectives in the " +
                              "policy catalogue are unverified claims until something does."
                            : null),

                    new DashboardMetric(
                        "Latest backup",
                        latestBackup?.TakenAt.ToString("O", CultureInfo.InvariantCulture) ?? "(none reported)",
                        latestBackup is null ? DashboardStatus.Unknown : DashboardStatus.Good)
                ],
                Module,
                "Unknown is not zero. These entries say the platform cannot see the thing, which is a " +
                "different situation from the thing being empty.")
        ];
    }

    private static DashboardStatus StatusOf(PlatformHealthState state) => state switch
    {
        PlatformHealthState.Healthy => DashboardStatus.Good,
        PlatformHealthState.Degraded => DashboardStatus.Warning,
        _ => DashboardStatus.Critical
    };
}

/// <summary>The cache board: hit ratios by namespace, and the errors a ratio would hide.</summary>
public sealed class CacheDashboardContributor : IDashboardContributor
{
    private readonly ICacheStatisticsProvider _cache;
    private readonly IOperationalPolicyCatalog _policies;

    public CacheDashboardContributor(ICacheStatisticsProvider cache, IOperationalPolicyCatalog policies)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(policies);

        _cache = cache;
        _policies = policies;
    }

    public string Dashboard => DashboardNames.Cache;

    public string Module => "Platform";

    public Task<IReadOnlyList<DashboardPanel>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var stats = _cache.Snapshot();

        IReadOnlyList<DashboardPanel> panels =
        [
            new DashboardPanel(
                "Overall",
                [
                    new DashboardMetric("Local store", stats.LocalProvider),
                    new DashboardMetric(
                        "Shared store",
                        stats.DistributedProvider ?? "(none)",
                        stats.IsDistributed ? DashboardStatus.Good : DashboardStatus.Unknown),
                    new DashboardMetric(
                        "Hit ratio",
                        stats.HitRatio.ToString("P1", CultureInfo.InvariantCulture),
                        RatioStatus(stats.HitRatio, stats.Hits + stats.Misses)),
                    new DashboardMetric(
                        "Errors",
                        stats.Errors.ToString(CultureInfo.InvariantCulture),
                        stats.Errors == 0 ? DashboardStatus.Good : DashboardStatus.Warning,
                        Hint: stats.Errors == 0
                            ? null
                            : "A healthy hit ratio with a rising error count means the shared tier is " +
                              "failing and the local tier is hiding it.")
                ],
                Module),

            new DashboardPanel(
                "By namespace",
                stats.Namespaces.Count == 0
                    ? [new DashboardMetric("Namespaces", "none used yet", DashboardStatus.Neutral)]
                    :
                    [
                        .. stats.Namespaces.Select(entry => new DashboardMetric(
                            entry.Namespace,
                            entry.HitRatio.ToString("P1", CultureInfo.InvariantCulture),
                            RatioStatus(entry.HitRatio, entry.Hits + entry.Misses),
                            Hint: $"{entry.Hits.ToString(CultureInfo.InvariantCulture)} hit / " +
                                  $"{entry.Misses.ToString(CultureInfo.InvariantCulture)} miss / " +
                                  $"{entry.StaleServes.ToString(CultureInfo.InvariantCulture)} stale / " +
                                  $"{entry.Errors.ToString(CultureInfo.InvariantCulture)} error"))
                    ],
                Module),

            new DashboardPanel(
                "Policies",
                [
                    .. _policies.All.OfType<CachePolicy>()
                        .Select(policy => new DashboardMetric(
                            policy.Name,
                            $"{policy.Tier} / {policy.AbsoluteExpiration}",
                            DashboardStatus.Neutral,
                            Hint: policy.Description))
                ],
                Module)
        ];

        return Task.FromResult(panels);
    }

    /// <summary>
    /// A hit ratio only means something once enough reads have happened.
    /// <para>
    /// Below a hundred reads the ratio is noise, and colouring it red would put a fresh instance in a
    /// warning state for its first few seconds every single deployment.
    /// </para>
    /// </summary>
    private static DashboardStatus RatioStatus(double ratio, long reads) => reads switch
    {
        < 100 => DashboardStatus.Neutral,
        _ when ratio >= 0.8 => DashboardStatus.Good,
        _ when ratio >= 0.5 => DashboardStatus.Neutral,
        _ => DashboardStatus.Warning
    };
}

/// <summary>The workers board: what is running, what has stalled, and what has never run.</summary>
public sealed class WorkerDashboardContributor : IDashboardContributor
{
    private readonly IWorkerRegistry _workers;

    public WorkerDashboardContributor(IWorkerRegistry workers)
    {
        ArgumentNullException.ThrowIfNull(workers);
        _workers = workers;
    }

    public string Dashboard => DashboardNames.Workers;

    public string Module => "Platform";

    public Task<IReadOnlyList<DashboardPanel>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var workers = _workers.All;

        IReadOnlyList<DashboardPanel> panels =
        [
            new DashboardPanel(
                "Workers",
                workers.Count == 0
                    ?
                    [
                        new DashboardMetric(
                            "Workers", "none started", DashboardStatus.Unknown,
                            Hint: "A registry with no entries means no worker has started on this " +
                                  "instance — which is a finding, not an absence of data.")
                    ]
                    :
                    [
                        .. workers.Select(worker => new DashboardMetric(
                            $"{worker.Module}/{worker.Name}",
                            worker.Lifecycle.ToString(),
                            worker.Lifecycle switch
                            {
                                WorkerLifecycle.Running => DashboardStatus.Good,
                                WorkerLifecycle.Recovering => DashboardStatus.Warning,
                                WorkerLifecycle.Faulted => DashboardStatus.Critical,
                                _ => DashboardStatus.Neutral
                            },
                            Hint: Describe(worker)))
                    ],
                Module)
        ];

        return Task.FromResult(panels);
    }

    private static string Describe(WorkerStatus worker)
    {
        var runs = worker.TotalRuns.ToString(CultureInfo.InvariantCulture);
        var items = worker.ItemsProcessed.ToString(CultureInfo.InvariantCulture);
        var last = worker.LastSuccessAt?.ToString("O", CultureInfo.InvariantCulture) ?? "never";

        var description = $"{runs} run(s), {items} item(s), last success {last}";

        return worker is { ConsecutiveFailures: > 0, LastError: { } error }
            ? $"{description}. {worker.ConsecutiveFailures.ToString(CultureInfo.InvariantCulture)} " +
              $"consecutive failure(s): {error}"
            : description;
    }
}

/// <summary>
/// The performance board: resilience state and the operational levers currently in force.
/// <para>
/// Breakers, bulkheads, flags and configuration on one board because that is the set an operator wants
/// when latency has changed and nothing was deployed. Nine times out of ten the answer is on this
/// board: a breaker opened, a flag flipped, or a setting was changed.
/// </para>
/// </summary>
public sealed class PerformanceDashboardContributor : IDashboardContributor
{
    private readonly ICircuitBreakerRegistry _breakers;
    private readonly IBulkheadRegistry _bulkheads;
    private readonly FeatureFlagService _flags;
    private readonly IRuntimeConfiguration _configuration;

    /// <summary>
    /// Takes the concrete <see cref="FeatureFlagService"/> rather than <c>IFeatureFlagService</c>.
    /// <para>
    /// That interface is served by a scoped per-request adapter, because evaluating a flag for the
    /// current caller needs the current caller. A dashboard panel has no caller to evaluate against —
    /// it lists what is declared and whether the kill switch is engaged — so it wants the singleton
    /// that holds the state, and taking the scoped surface would make this contributor scoped for no
    /// reason.
    /// </para>
    /// </summary>
    public PerformanceDashboardContributor(
        ICircuitBreakerRegistry breakers,
        IBulkheadRegistry bulkheads,
        FeatureFlagService flags,
        IRuntimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(breakers);
        ArgumentNullException.ThrowIfNull(bulkheads);
        ArgumentNullException.ThrowIfNull(flags);
        ArgumentNullException.ThrowIfNull(configuration);

        _breakers = breakers;
        _bulkheads = bulkheads;
        _flags = flags;
        _configuration = configuration;
    }

    public string Dashboard => DashboardNames.Performance;

    public string Module => "Platform";

    public async Task<IReadOnlyList<DashboardPanel>> BuildAsync(
        CancellationToken cancellationToken = default)
    {
        var killSwitch = await _flags.GetKillSwitchAsync(cancellationToken).ConfigureAwait(false);
        var flags = await _flags.ListAsync(cancellationToken).ConfigureAwait(false);
        var overridden = _configuration.All.Where(entry => entry.IsOverridden).ToArray();

        return
        [
            new DashboardPanel(
                "Circuit breakers",
                _breakers.All.Count == 0
                    ? [new DashboardMetric("Breakers", "none exercised", DashboardStatus.Neutral)]
                    :
                    [
                        .. _breakers.All.Select(breaker => new DashboardMetric(
                            breaker.Dependency,
                            breaker.State.ToString(),
                            breaker.State switch
                            {
                                CircuitState.Closed => DashboardStatus.Good,
                                CircuitState.HalfOpen => DashboardStatus.Warning,
                                _ => DashboardStatus.Critical
                            },
                            Hint: $"{breaker.FailureRatio.ToString("P0", CultureInfo.InvariantCulture)} of " +
                                  $"{breaker.Throughput.ToString(CultureInfo.InvariantCulture)} call(s) " +
                                  $"failing; {breaker.TotalTrips.ToString(CultureInfo.InvariantCulture)} trip(s) since start"))
                    ],
                Module),

            new DashboardPanel(
                "Bulkheads",
                _bulkheads.All.Count == 0
                    ? [new DashboardMetric("Bulkheads", "none exercised", DashboardStatus.Neutral)]
                    :
                    [
                        .. _bulkheads.All.Select(bulkhead => new DashboardMetric(
                            bulkhead.Name,
                            $"{bulkhead.InUse.ToString(CultureInfo.InvariantCulture)}/" +
                            $"{bulkhead.Capacity.ToString(CultureInfo.InvariantCulture)}",
                            bulkhead.InUse >= bulkhead.Capacity
                                ? DashboardStatus.Warning
                                : DashboardStatus.Good,
                            Hint: $"{bulkhead.Queued.ToString(CultureInfo.InvariantCulture)} queued, " +
                                  $"{bulkhead.TotalRejections.ToString(CultureInfo.InvariantCulture)} rejected"))
                    ],
                Module),

            new DashboardPanel(
                "Operational levers",
                [
                    new DashboardMetric(
                        "Kill switch",
                        killSwitch.IsEngaged ? "ENGAGED" : "released",
                        killSwitch.IsEngaged ? DashboardStatus.Critical : DashboardStatus.Good,
                        Hint: killSwitch.IsEngaged
                            ? $"By {killSwitch.EngagedBy} at {killSwitch.EngagedAt:O}: {killSwitch.Reason}"
                            : null),

                    new DashboardMetric(
                        "Feature flags",
                        flags.Count.ToString(CultureInfo.InvariantCulture)),

                    new DashboardMetric(
                        "Overridden settings",
                        overridden.Length.ToString(CultureInfo.InvariantCulture),
                        overridden.Length == 0 ? DashboardStatus.Neutral : DashboardStatus.Warning,
                        Hint: overridden.Length == 0
                            ? null
                            : string.Join(", ", overridden.Select(entry => entry.Definition.Key)))
                ],
                Module,
                "When latency changed and nothing was deployed, the answer is usually here.")
        ];
    }
}
