using Diaspora.Modules.Providers.Application.EventHandlers;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Modules.Providers.Extensibility.Availability;
using Diaspora.Modules.Providers.Extensibility.Categories;
using Diaspora.Modules.Providers.Extensibility.Pricing;
using Diaspora.Modules.Providers.Extensibility.Ranking;
using Diaspora.Modules.Providers.Extensibility.Recommendations;
using Diaspora.Modules.Providers.Extensibility.Search;
using Diaspora.Modules.Providers.Extensibility.Types;
using Diaspora.Modules.Providers.Extensibility.Verification;
using Diaspora.Modules.Providers.Infrastructure.Hosting;
using Diaspora.Modules.Providers.Integration;
using Diaspora.Modules.Providers.Options;
using Diaspora.Modules.Providers.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Providers;

/// <summary>
/// The Provider &amp; Service Ecosystem Engine.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so
/// it lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor
/// scan. <c>Program.cs</c>, the Platform Kernel, Identity, Trust, Location, Media and Community
/// are untouched.
/// </para>
/// <para>
/// <b>Order 600.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300), Media
/// (400) and Community (500). Registration order is not load-bearing — every cross-module
/// dependency here is resolved lazily through the container — but ordering last matches the
/// dependency direction: Providers consumes all six and is consumed by every business module
/// after it.
/// </para>
/// <para>
/// <b>Eight extension seams are assembly-scanned</b>, which is the architectural claim of this
/// module: a category tree, a verification source, an availability source, a price formatter, a
/// ranking strategy, a search signal, a recommender and a provider type are each a class in
/// whichever assembly owns the idea. This file gains no line when one is added.
/// </para>
/// </summary>
public sealed class ProvidersModule : IPlatformModule
{
    public string Name => ProvidersModuleMetadata.Name;

    public string Version => ProvidersModuleMetadata.Version;

    public int Order => 600;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(ProvidersModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviors
        // are open generics registered once, so validation, telemetry, transactions,
        // authorisation and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<ProviderOptions>()
            .Bind(configuration.GetSection(ProviderOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterConsumerContracts(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<IServiceCategoryRepository, ServiceCategoryRepository>();
        services.AddScoped<IProviderReviewRepository, ProviderReviewRepository>();
        services.AddScoped<IProviderRecommendationRepository, ProviderRecommendationRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<ProviderAuthorizationService>();
        services.AddScoped<ServiceCategoryResolver>();
        services.AddScoped<ProviderSlugAllocator>();
        services.AddScoped<GeographicScopeAdapter>();
        services.AddScoped<ProviderAreaFiling>();
        services.AddScoped<AvailabilityCalculator>();
        services.AddScoped<ReviewSummaryService>();
        services.AddScoped<ProviderReviewerNames>();
        services.AddScoped<ProviderMediaClaimer>();
        services.AddScoped<ProviderBookmarkService>();
        services.AddScoped<ProviderReadContextBuilder>();

        services.AddScoped<PricingMapper>();
        services.AddScoped<ProviderMapper>();

        services.AddScoped<ProviderIntegrationEventRelay>();
        services.AddScoped<TrustVerificationProjector>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new ranking is a new class" literally true.
    /// A module ships an assembly and its categories, verifiers, availability sources, price
    /// formatters, rankings, search signals, recommenders and provider types appear; this file
    /// does not change.
    /// </para>
    /// <para>
    /// <b>Everything here is scoped, including the registries.</b> Nearly every real seam will
    /// hold a repository or an HTTP client, and a singleton registry over scoped
    /// implementations is the captive dependency the host's provider validation refuses to start
    /// on. B4 recorded that exact failure with its country metadata catalogue and B6 repeated the
    /// note; paying one dictionary construction per request is a small price for not repeating it
    /// a third time.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IServiceCategoryContributor>(services, assemblies);
        RegisterAll<IProviderTypeContributor>(services, assemblies);
        RegisterAll<IProviderVerificationProvider>(services, assemblies);
        RegisterAll<IProviderAvailabilityProvider>(services, assemblies);
        RegisterAll<IProviderPricingFormatter>(services, assemblies);
        RegisterAll<IProviderRankingStrategy>(services, assemblies);
        RegisterAll<IProviderSearchProjectionContributor>(services, assemblies);
        RegisterAll<IProviderRecommendationProvider>(services, assemblies);

        services.AddScoped<ProviderTypeRegistry>();
        services.AddScoped<ProviderVerificationProviderRegistry>();
        services.AddScoped<AvailabilityProviderRegistry>();
        services.AddScoped<PricingFormatterRegistry>();
        services.AddScoped<RankingStrategyRegistry>();
    }

    /// <summary>
    /// The consumer contracts other modules bind to.
    /// <para>
    /// This is the surface the brief promises Marketplace, Jobs, Housing, Events, Community Life
    /// and Guides: resolve a provider, render its card, check a role, confirm a badge, read the
    /// category tree, ingest a search document. Nothing else of B7 is public to them, and none of
    /// them needs a provider table of its own.
    /// </para>
    /// </summary>
    private static void RegisterConsumerContracts(IServiceCollection services)
    {
        services.AddScoped<IProviderDirectory, ProviderDirectoryService>();
        services.AddScoped<IProviderCatalog, ProviderCatalogService>();
        services.AddScoped<IProviderSearchProjectionSource, ProviderSearchProjectionService>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<ServiceCategorySeeder>();
        services.AddHostedService<ProviderSweeper>();
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
