using Diaspora.Operations.Alerting;
using Diaspora.Operations.Caching;
using Diaspora.Operations.Configuration;
using Diaspora.Operations.Dashboards;
using Diaspora.Operations.Diagnostics;
using Diaspora.Operations.FeatureManagement;
using Diaspora.Operations.Health;
using Diaspora.Operations.Privacy;
using Diaspora.Operations.RateLimiting;
using Diaspora.Operations.Recovery;
using Diaspora.Operations.Resilience;
using Diaspora.Operations.Scalability;
using Diaspora.Operations.Scheduling;
using Diaspora.Operations.Search;
using Diaspora.Operations.Workers;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Configuration;
using Diaspora.Platform.Operations.Dashboards;
using Diaspora.Platform.Operations.Diagnostics;
using Diaspora.Platform.Operations.FeatureManagement;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.Platform.Operations.Recovery;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Operations.Scalability;
using Diaspora.Platform.Operations.Scheduling;
using Diaspora.Platform.Operations.Search;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Workers;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diaspora.Operations;

/// <summary>
/// Wires the Production Platform into a host.
/// <para>
/// One call, after the kernel and after the modules. After the kernel because the operational policy
/// catalogue is composed there; after the modules because several registrations here are
/// <c>TryAdd</c>-shaped defaults that a module or a deployment may already have replaced.
/// </para>
/// <para>
/// <b>Every default here is replaceable and none of them lies.</b> The cache is real and local-only;
/// the lock is real and per-process; the search provider, backup provider and fault injector all report
/// that they are not configured rather than pretending to work. That distinction is the difference
/// between a platform that degrades visibly and one that appears healthy while doing nothing.
/// </para>
/// </summary>
public static class OperationsRegistration
{
    public static IServiceCollection AddProductionPlatform(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);

        AddCaching(services);
        AddResilience(services);
        AddRateLimiting(services);
        AddFeatureManagement(services);
        AddRuntimeConfiguration(services);
        AddScheduling(services);
        AddSearch(services);
        AddSecurity(services);
        AddScalability(services);
        AddRecoveryAndPrivacy(services);
        AddHealthAndDashboards(services);
        AddWorkers(services);

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<TopologyOptions>()
            .Bind(configuration.GetSection(TopologyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddCaching(IServiceCollection services)
    {
        // The local tier is registered unconditionally and is the only store that ships. A deployment
        // adds a shared store by registering another ICacheStore whose Tier is Distributed; the cache
        // takes the last one registered, so ordering is the whole configuration story.
        services.AddSingleton<MemoryCacheStore>();
        services.AddSingleton<ICacheStore>(provider => provider.GetRequiredService<MemoryCacheStore>());

        // The version store writes through the shared tier when there is one, because a namespace or
        // tag version that lived only in this process would invalidate one replica's cache and no
        // others. Falling back to the local store keeps single-instance behaviour exactly correct.
        services.AddSingleton(provider =>
        {
            var stores = provider.GetServices<ICacheStore>().ToArray();

            var backing = stores.LastOrDefault(store => store.Tier == CacheStoreTier.Distributed)
                ?? stores.Last(store => store.Tier == CacheStoreTier.Local);

            return new CacheVersionStore(
                backing,
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<IOptions<CacheOptions>>());
        });

        services.AddSingleton<PlatformCacheService>();
        services.AddSingleton<ICacheService>(provider => provider.GetRequiredService<PlatformCacheService>());
        services.AddSingleton<ICacheInvalidator>(provider => provider.GetRequiredService<PlatformCacheService>());
        services.AddSingleton<ICacheStatisticsProvider>(provider => provider.GetRequiredService<PlatformCacheService>());

        services.AddHostedService<CacheWarmupHostedService>();
    }

    private static void AddResilience(IServiceCollection services)
    {
        services.AddSingleton<IFailureClassifier, DefaultFailureClassifier>();

        services.AddSingleton<ResilienceExecutor>();
        services.AddSingleton<IResilienceExecutor>(provider => provider.GetRequiredService<ResilienceExecutor>());
        services.AddSingleton<ICircuitBreakerRegistry>(provider => provider.GetRequiredService<ResilienceExecutor>());
        services.AddSingleton<IBulkheadRegistry>(provider => provider.GetRequiredService<ResilienceExecutor>());

        services.TryAddSingleton<IFaultInjector, NoFaultInjector>();
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddSingleton<PlatformRateLimiter>();
        services.AddSingleton<IPlatformRateLimiter>(provider => provider.GetRequiredService<PlatformRateLimiter>());

        // Replaces Infrastructure's InMemorySubjectRateLimiter so that every limit on the platform is
        // enforced by one implementation, against one policy catalogue, reported through one metric.
        // Two limiters would be two sets of numbers that disagree, and B17-R4 exists to prevent that.
        services.AddSingleton<ISubjectRateLimiter, OperationsSubjectRateLimiter>();
    }

    private static void AddFeatureManagement(IServiceCollection services)
    {
        // The state is a singleton and the per-caller surface is scoped. Registering IFeatureFlagService
        // on the singleton would capture ICurrentUser — a scoped service that reads this request's
        // claims — and the container's scope validation refuses to build that graph. It was right to:
        // a captured scoped service is resolved once at startup, so every flag evaluation for the life
        // of the process would be made against whoever happened to be first.
        services.AddSingleton<FeatureFlagService>();
        services.AddSingleton<IFeatureFlagAdministration>(provider => provider.GetRequiredService<FeatureFlagService>());
        services.AddScoped<IFeatureFlagService, ScopedFeatureFlagService>();
    }

    private static void AddRuntimeConfiguration(IServiceCollection services)
    {
        services.AddSingleton<RuntimeConfigurationService>();
        services.AddSingleton<IRuntimeConfiguration>(provider => provider.GetRequiredService<RuntimeConfigurationService>());
        services.AddSingleton<IRuntimeConfigurationAdministration>(provider => provider.GetRequiredService<RuntimeConfigurationService>());
    }

    private static void AddScheduling(IServiceCollection services)
    {
        services.AddSingleton<InMemoryJobScheduler>();
        services.AddSingleton<IBackgroundJobScheduler>(provider => provider.GetRequiredService<InMemoryJobScheduler>());

        services.TryAddSingleton<IDistributedLock, InProcessDistributedLock>();
    }

    private static void AddSearch(IServiceCollection services)
    {
        // TryAdd, so a registered adapter wins. Both interfaces come from the same instance: a
        // deployment with a reader and no writer would silently stop indexing.
        services.TryAddSingleton<UnavailableSearchProvider>();
        services.TryAddSingleton<ISearchProvider>(provider => provider.GetRequiredService<UnavailableSearchProvider>());
        services.TryAddSingleton<ISearchIndexer>(provider => provider.GetRequiredService<UnavailableSearchProvider>());
    }

    private static void AddSecurity(IServiceCollection services)
    {
        services.AddSingleton<Security.ConfigurationSecretStore>();
        services.AddSingleton<ISecretStore>(provider => provider.GetRequiredService<Security.ConfigurationSecretStore>());

        services.AddSingleton<Security.RecordingThreatSignalSink>();
        services.AddSingleton<IThreatSignalSink>(provider => provider.GetRequiredService<Security.RecordingThreatSignalSink>());
    }

    private static void AddScalability(IServiceCollection services)
    {
        services.TryAddSingleton<IRegionContext, SingleRegionContext>();
        services.TryAddSingleton<IContentDeliveryUrlBuilder, OriginContentDeliveryUrlBuilder>();

        // Scoped, because read-your-own-writes is a property of one unit of work. A singleton router
        // would let one request's write force every other request onto the writer, and — worse — a
        // request that had not written would inherit a consistency requirement it did not create.
        services.TryAddScoped<IReadReplicaRouter, PrimaryOnlyReadReplicaRouter>();
    }

    private static void AddRecoveryAndPrivacy(IServiceCollection services)
    {
        services.TryAddSingleton<IBackupProvider, UnconfiguredBackupProvider>();
        services.AddSingleton<IPrivacyAuditor, PrivacyAuditor>();
    }

    private static void AddHealthAndDashboards(IServiceCollection services)
    {
        services.AddSingleton<IPlatformHealthAggregator, PlatformHealthAggregator>();
        services.AddSingleton<IModuleHealthContributor, OperationsHealthContributor>();

        services.AddSingleton<IOperationalDashboardService, OperationalDashboardService>();
        services.AddSingleton<IDashboardContributor, PlatformHealthDashboardContributor>();
        services.AddSingleton<IDashboardContributor, CacheDashboardContributor>();
        services.AddSingleton<IDashboardContributor, WorkerDashboardContributor>();
        services.AddSingleton<IDashboardContributor, PerformanceDashboardContributor>();

        services.AddSingleton<IAlertRuleCatalog, AlertRuleCatalog>();
        services.AddSingleton<IAlertRuleContributor, PlatformAlertRuleContributor>();
    }

    private static void AddWorkers(IServiceCollection services)
    {
        services.AddSingleton<IWorkerRegistry, WorkerRegistry>();

        services.AddPlatformWorker<JobExecutionWorker>();
        services.AddPlatformWorker<OperationsMaintenanceWorker>();
        services.AddPlatformWorker<ThreatDetectionWorker>();
        services.AddPlatformWorker<RetentionWorker>();
        services.AddPlatformWorker<SearchIndexingWorker>();
    }

    /// <summary>
    /// Registers the Production Platform's own contributors with the kernel's assembly scan.
    /// <para>
    /// Called by the host only when it is scanning a fixed assembly list — the normal path finds these
    /// through <see cref="ModuleAssemblyScanner"/> like every other contributor, because
    /// <c>Diaspora.Operations</c> is a <c>Diaspora.</c> assembly and the scan does not special-case it.
    /// </para>
    /// </summary>
    public static IServiceCollection AddProductionPlatformContributors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<Platform.Operations.Policies.IOperationalPolicyContributor,
            Policies.PlatformResiliencePolicyContributor>();

        services.AddSingleton<Platform.Operations.Policies.IOperationalPolicyContributor,
            Policies.PlatformOperationalPolicyContributor>();

        return services;
    }
}
