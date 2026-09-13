using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Application.Projection;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Infrastructure.Hosting;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Modules.Analytics.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Modules.Analytics;

/// <summary>
/// The Analytics Platform's registration.
/// <para>
/// <b>Order 1800, last, and the number is load-bearing in one direction only.</b> This module
/// consumes published contracts from every module that produces one, and consumers are resolved
/// from the container rather than from a registry built in module order — so nothing here strictly
/// requires composing after B18. What the number states is the dependency direction: analytics is
/// downstream of everything and upstream of nothing, and a module whose order was lower would be a
/// module something else could plausibly come to depend on.
/// </para>
/// <para>
/// <b>It references no other module, and an architecture test says so.</b> Every fact it aggregates
/// arrives as a contract in <c>Diaspora.Contracts</c>, published by a module that has never heard of
/// analytics. That is not tidiness: a module reference would let this one read another module's
/// <em>current</em> state, and analytics that could do that would stop being a record of what
/// happened and become a second answer to what is true now — the exact failure §4 of the phase brief
/// forbids.
/// </para>
/// <para>
/// <b>What it does not register is as much the point as what it does.</b> No money, no wallet, no
/// ledger, no recommendation engine, no notification service, no second identity store, no second
/// idempotency mechanism and no second cache. Each of those exists on this platform already or
/// belongs to a phase that has not run.
/// </para>
/// </summary>
public sealed class AnalyticsModule : IPlatformModule
{
    public string Name => AnalyticsModuleMetadata.Name;

    public string Version => AnalyticsModuleMetadata.Version;

    /// <summary>See the class summary: downstream of everything, upstream of nothing.</summary>
    public int Order => 1800;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(AnalyticsModule).Assembly;

        // Handlers, consumers and validators from this assembly. The Application layer's pipeline
        // behaviours are open generics registered once, so validation, telemetry, transactions,
        // authorisation and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<AnalyticsOptions>()
            .Bind(configuration.GetSection(AnalyticsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IAnalyticsEventRepository, AnalyticsEventRepository>();
        services.AddScoped<IAnalyticsContentDimensionRepository, AnalyticsContentDimensionRepository>();
        services.AddScoped<IAnalyticsRollupRepository, AnalyticsRollupRepository>();
        services.AddScoped<IAnalyticsProjectionRepository, AnalyticsProjectionRepository>();
        services.AddScoped<IAnalyticsEventSchemaRepository, AnalyticsEventSchemaRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // The one place a moment becomes a calendar day. Singleton because the resolved timezone is
        // deployment state rather than request state, and resolving it per request would put a zone
        // database lookup on the ingestion path.
        services.AddSingleton<AnalyticsReportingCalendar>();

        // The keyed hash. Singleton because the key is configuration and hashing is stateless.
        services.AddSingleton<AnalyticsActorHasher>();

        // The one door into the raw store — twelve consumers and one endpoint go through it, so the
        // deduplication rule and the trust classification are written once.
        services.AddScoped<AnalyticsIngestionService>();
        services.AddScoped<AnalyticsDimensionRecorder>();

        // The one door to "who is calling and what may they see".
        services.AddScoped<AnalyticsQueryContext>();
        services.AddScoped<AnalyticsReadService>();
        services.AddScoped<AnalyticsDatasetBuilder>();

        // The seller, landlord and employer dashboards — one question asked of three content types,
        // answered once.
        services.AddScoped<AnalyticsOwnerDatasetComposer>();

        // Registered explicitly rather than scanned: it is a collaborator of three workers and of the
        // backfill, not a seam anything plugs into.
        services.AddScoped<AnalyticsProjectionService>();

        // B23.x Wave 12. This module's answer to `metricAbove` and `metricBelow`, which the automation
        // platform declared unevaluable in Wave 11 with the words "analytics figures are B19's and
        // this module has no read port for them". Read-only, over the same rollups every creator
        // surface reads, so an automation's threshold and a member's dashboard cannot disagree.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationConditionSource,
            Extensibility.Automation.AnalyticsAutomationConditionSource>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        // Discovered rather than listed, since PSR-1: the integration harness resolves whatever is
        // registered and runs it in module order, so this registry arrives there with no edit.
        services.AddReferenceDataSeeder<AnalyticsSchemaSeeder>();

        // All three take B17's IDistributedLock before touching anything, and all three have a
        // configuration switch — DEBT-049's lesson, applied on the module's first day rather than
        // four phases in.
        services.AddHostedService<AnalyticsProjectionWorker>();
        services.AddHostedService<AnalyticsRetentionWorker>();
        services.AddHostedService<AnalyticsBackfillWorker>();
    }
}
