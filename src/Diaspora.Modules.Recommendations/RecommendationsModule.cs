using Diaspora.Modules.Recommendations.Application.EventHandlers;
using Diaspora.Modules.Recommendations.Application.Projection;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Extensibility.Candidates;
using Diaspora.Modules.Recommendations.Extensibility.Catalog;
using Diaspora.Modules.Recommendations.Extensibility.ColdStart;
using Diaspora.Modules.Recommendations.Extensibility.Diversity;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Explainability;
using Diaspora.Modules.Recommendations.Extensibility.Feed;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Extensibility.Policies;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Modules.Recommendations.Extensibility.Search;
using Diaspora.Modules.Recommendations.Extensibility.Signals;
using Diaspora.Modules.Recommendations.Extensibility.Similarity;
using Diaspora.Modules.Recommendations.Infrastructure.Caching;
using Diaspora.Modules.Recommendations.Infrastructure.Candidates;
using Diaspora.Modules.Recommendations.Infrastructure.ColdStart;
using Diaspora.Modules.Recommendations.Infrastructure.Diversity;
using Diaspora.Modules.Recommendations.Infrastructure.Engine;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;
using Diaspora.Modules.Recommendations.Infrastructure.Feed;
using Diaspora.Modules.Recommendations.Infrastructure.Hosting;
using Diaspora.Modules.Recommendations.Infrastructure.Personalization;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;
using Diaspora.Modules.Recommendations.Infrastructure.Ranking;
using Diaspora.Modules.Recommendations.Infrastructure.Search;
using Diaspora.Modules.Recommendations.Infrastructure.Signals;
using Diaspora.Modules.Recommendations.Infrastructure.Similarity;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Modules.Recommendations.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Recommendations;

/// <summary>
/// B14's registration.
/// <para>
/// <b>Order 1300, last.</b> The Recommendation Platform consumes what every other module publishes
/// and is consumed by none of them, so it belongs at the end of the graph — and the seam registries
/// are built in module order, which means a ranking strategy, a policy or a feed section contributed
/// by an earlier module is already present when this one scans for them.
/// </para>
/// <para>
/// <b>The seams are scanned; everything else is registered by hand.</b> A ranking strategy, a policy
/// rule, a candidate source, a catalogue contributor or a feed section is a seam and may arrive from
/// any assembly — that is the whole extensibility claim. A repository, the engine, the profile
/// builder or the projector is a collaborator of this module's own handlers, and scanning for those
/// would let an accidental second implementation elsewhere silently take over a write path.
/// </para>
/// <para>
/// <b>The single-implementation seams are registered last-wins.</b> The shipped reason builder,
/// diversifier, cold-start provider, similarity provider, signal store, weight policy, cache, search
/// provider, feed composer and engine are all replaceable: a deployment registers its own after this
/// module and <c>AddScoped</c>'s last-wins behaviour does the rest. The plural seams — strategies,
/// policy rules, candidate sources, contributors — are additive and every implementation runs.
/// </para>
/// </summary>
public sealed class RecommendationsModule : IPlatformModule
{
    public string Name => RecommendationsModuleMetadata.Name;

    public string Version => RecommendationsModuleMetadata.Version;

    public int Order => 1300;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(RecommendationsModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<RecommendationOptions>()
            .Bind(configuration.GetSection(RecommendationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ISignalRepository, SignalRepository>();
        services.AddScoped<IPersonalizationProfileRepository, PersonalizationProfileRepository>();
        services.AddScoped<IPersonalizationPreferenceRepository, PersonalizationPreferenceRepository>();
        services.AddScoped<IRecommendationCandidateRepository, RecommendationCandidateRepository>();
        services.AddScoped<IRecommendationSetRepository, RecommendationSetRepository>();
        services.AddScoped<IRecommendationFeedbackRepository, RecommendationFeedbackRepository>();
        services.AddScoped<IRecommendationPolicyRepository, RecommendationPolicyRepository>();
        services.AddScoped<IRankingStrategyRepository, RankingStrategyRepository>();
        services.AddScoped<IRecommendationAnalyticsRepository, RecommendationAnalyticsRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<RecommendationAuthorizationService>();
        services.AddScoped<PersonalizationContextFactory>();

        // The one door a published contract goes through to become a candidate row. Registered
        // explicitly rather than scanned because a second implementation appearing by accident would
        // be the defect hardest to notice — a vertical quietly projecting itself differently.
        services.AddScoped<CandidateProjector>();

        // The single translation point where a domain fact becomes a published contract.
        services.AddScoped<RecommendationIntegrationEventRelay>();
    }

    /// <summary>
    /// The extension points, scanned across every platform assembly.
    /// <para>
    /// <b>The plural seams are additive.</b> Every registered ranking strategy is available to the
    /// catalogue, every policy rule to the evaluator, every candidate source to the engine, and every
    /// contributor to the seeder. A module adding one does not displace anything.
    /// </para>
    /// <para>
    /// <b>The singular seams are last-wins.</b> A deployment replaces the reason builder, the
    /// diversifier or the similarity provider by registering its own after this module — which is
    /// what <c>AddScoped</c> gives, and is how an embedding-based similarity or a machine-learned
    /// ranker would land without a line of this module changing.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        // Reference data. Any module may contribute strategies, policies and feed sections without
        // this module referencing it.
        RegisterAll<IRankingStrategyContributor>(services, assemblies);
        RegisterAll<IRecommendationPolicyContributor>(services, assemblies);
        RegisterAll<IFeedSectionContributor>(services, assemblies);

        // Ranking. Every implementation is available; the catalogue decides which run where.
        RegisterAll<IRankingStrategy>(services, assemblies);
        services.AddScoped<IRankingStrategyRegistry, RankingStrategyRegistry>();

        // Policy. Every rule is available; the catalogue decides which are enabled.
        RegisterAll<IRecommendationPolicyRule>(services, assemblies);
        services.AddScoped<RecommendationPolicyRuleRegistry>();
        services.AddScoped<IRecommendationPolicyEvaluator, RecommendationPolicyEvaluator>();

        // Retrieval. Additive — a vector adapter runs alongside the projection, not instead of it.
        RegisterAll<IRecommendationCandidateSource>(services, assemblies);

        // The replaceable singles. See the method summary.
        services.AddScoped<ISimilarityProvider, AttributeOverlapSimilarityProvider>();
        services.AddScoped<IRecommendationReasonBuilder, DefaultRecommendationReasonBuilder>();
        services.AddScoped<IRecommendationDiversifier, GroupCappingDiversifier>();
        services.AddScoped<IColdStartProvider, LocationLadderColdStartProvider>();
        services.AddScoped<IPersonalizationProfileBuilder, DefaultPersonalizationProfileBuilder>();
        services.AddScoped<ISignalWeightPolicy, DefaultSignalWeightPolicy>();
        services.AddScoped<ISignalStore, DefaultSignalStore>();
        services.AddScoped<ISearchSignalProvider, ProfileSearchSignalProvider>();
        services.AddScoped<IFeedComposer, DefaultFeedComposer>();

        // The one door into a recommendation. An architecture test asserts there is exactly one
        // implementation in this assembly — see IRecommendationEngine for why that matters.
        services.AddScoped<IRecommendationEngine, RecommendationEngine>();

        // A singleton, and safe as such: a concurrent dictionary, no scoped dependency, and the one
        // component whose whole value is that it outlives a request.
        services.AddSingleton<IRecommendationCache, InMemoryRecommendationCache>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<RecommendationCatalogueSeeder>();
        services.AddHostedService<ProfileRefreshSweep>();
        services.AddHostedService<RecommendationRetentionSweep>();
        services.AddHostedService<AnalyticsRollupSweep>();
        services.AddHostedService<CandidatePruningSweep>();
    }

    private static void RegisterAll<TContract>(
        IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            services.AddScoped(typeof(TContract), type);
        }
    }
}
