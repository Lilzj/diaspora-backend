using Diaspora.Modules.Housing.Application.EventHandlers;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Extensibility.Catalog;
using Diaspora.Modules.Housing.Extensibility.Fraud;
using Diaspora.Modules.Housing.Extensibility.Neighborhoods;
using Diaspora.Modules.Housing.Extensibility.Privacy;
using Diaspora.Modules.Housing.Extensibility.Ranking;
using Diaspora.Modules.Housing.Extensibility.Search;
using Diaspora.Modules.Housing.Extensibility.Views;
using Diaspora.Modules.Housing.Infrastructure.Hosting;
using Diaspora.Modules.Housing.Infrastructure.Neighborhoods;
using Diaspora.Modules.Housing.Infrastructure.Search;
using Diaspora.Modules.Housing.Infrastructure.Statistics;
using Diaspora.Modules.Housing.Infrastructure.Trust;
using Diaspora.Modules.Housing.Infrastructure.Views;
using Diaspora.Modules.Housing.Integration;
using Diaspora.Modules.Housing.Options;
using Diaspora.Modules.Housing.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Housing;

/// <summary>
/// The Housing, Accommodation &amp; Neighborhood Platform.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor scan.
/// <b>No edit was needed to the Platform Kernel, <c>Program.cs</c>, Identity, Trust, Location,
/// Media, Community, Providers, Guides, Jobs or Marketplace</b> — the module plugs in, and the
/// modules it consumes do not learn that it exists.
/// </para>
/// <para>
/// <b>Unlike the last three phases, this one deletes nothing from the host.</b> B8 moved
/// <c>guides.publish</c> out of the provisional catalogue, B9 moved <c>jobs.apply</c> and B10 moved
/// <c>marketplace.browse</c>; the host has never declared a housing gate, so
/// <c>ProvisionalCatalogContributor</c> is untouched. That was checked rather than assumed — the
/// feature catalogue refuses two modules declaring one gate, and the resulting startup failure
/// takes down every request that resolves it.
/// </para>
/// <para>
/// <b>Order 1000.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300), Media (400),
/// Community (500), Providers (600), Guides (700), Jobs (800) and Marketplace (900). Registration
/// order is not load-bearing — every cross-module dependency resolves lazily through the
/// container — but ordering last matches the dependency direction: Housing consumes five modules
/// and is consumed by nothing yet.
/// </para>
/// <para>
/// <b>Eight extension seams are assembly-scanned</b>, which is the architectural claim of this
/// module: an accommodation type, an amenity, a ranking strategy, a fraud signal source, a search
/// engine, a view tracker, a neighbourhood metric source and an address-disclosure policy are each
/// a class in whichever assembly owns the idea. This file gains no line when one is added.
/// </para>
/// </summary>
public sealed class HousingModule : IPlatformModule
{
    public string Name => HousingModuleMetadata.Name;

    public string Version => HousingModuleMetadata.Version;

    public int Order => 1000;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(HousingModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<HousingOptions>()
            .Bind(configuration.GetSection(HousingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterIntegration(services);
        RegisterHostedServices(services);
    }

    /// <summary>
    /// One repository per aggregate, plus the unit-reservation guard.
    /// <para>
    /// <b>The guard is registered here rather than as a seam, and that is deliberate.</b> It is not
    /// a contribution point: there is one correct way to arbitrate a hold in this deployment, and it
    /// changes when the platform grows a guarded atomic counter — not when somebody ships an
    /// assembly. See <see cref="IHousingUnitReservationGuard"/>.
    /// </para>
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IHousingListingRepository, HousingListingRepository>();

        // Cross-module content seams in Diaspora.Contracts: the platform overview's counts and
        // a sponsored item's title. Resolved as collections by Analytics and Creator.
        services.AddScoped<Diaspora.Contracts.Content.V1.IPlatformInventorySource, Persistence.HousingPublishedContentSource>();
        services.AddScoped<Diaspora.Contracts.Content.V1.IContentTitleSource, Persistence.HousingPublishedContentSource>();

        // How a Trust moderation case carries its decision out on a property (7 Nov 2026).
        services.AddScoped<Diaspora.Contracts.Moderation.V1.IModerationTarget, Infrastructure.Moderation.HousingListingModerationTarget>();
        services.AddScoped<ILandlordProfileRepository, LandlordProfileRepository>();
        services.AddScoped<IHousingViewingRepository, HousingViewingRepository>();
        services.AddScoped<IHousingReservationRepository, HousingReservationRepository>();
        services.AddScoped<ILandlordReviewRepository, LandlordReviewRepository>();
        services.AddScoped<INeighborhoodProfileRepository, NeighborhoodProfileRepository>();
        services.AddScoped<INeighborhoodInsightRepository, NeighborhoodInsightRepository>();
        services.AddScoped<IAccommodationTypeRepository, AccommodationTypeRepository>();
        services.AddScoped<IHousingAmenityRepository, HousingAmenityRepository>();
        services.AddScoped<IHousingReportRepository, HousingReportRepository>();
        services.AddScoped<IHousingAnalyticsRepository, HousingAnalyticsRepository>();

        services.AddScoped<IHousingUnitReservationGuard, EfHousingUnitReservationGuard>();
    }

    /// <summary>
    /// The application services handlers compose.
    /// <para>
    /// The five cross-module adapters are here rather than in <c>Infrastructure</c> because they are
    /// consumers rather than contributions: the media claimer, the geographic adapter, the
    /// neighbourhood resolver, the provider resolver, the guide resolver and the Trust submission
    /// provider each know exactly one foreign module, and nothing else in B11 does. That containment
    /// is the rule the architecture tests enforce.
    /// </para>
    /// <para>
    /// Stateless helpers — every mapper — are static classes and are deliberately absent:
    /// registering a type with no state is a container entry nobody resolves, and CA1822 in Release
    /// is what pushed them static in the first place.
    /// </para>
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<HousingAuthorizationService>();
        services.AddScoped<LandlordReviewSummaryService>();
        services.AddScoped<HousingListingWriteGate>();
        services.AddScoped<HousingListingReadContext>();
        services.AddScoped<HousingCatalogResolver>();
        services.AddScoped<HousingSlugAllocator>();
        services.AddScoped<HousingFraudAssessor>();
        services.AddScoped<LandlordListingCountRefresher>();
        services.AddScoped<NeighborhoodListingCountRefresher>();
        services.AddScoped<HousingAddressDisclosureService>();
        services.AddScoped<HousingSearchFacetMapper>();
        services.AddScoped<NeighborhoodStatisticsService>();

        // The cross-module adapters. One class per foreign module, and no other class in B11 holds
        // a reference to any of them.
        services.AddScoped<HousingMediaClaimer>();
        services.AddScoped<HousingGeographicScopeAdapter>();
        services.AddScoped<HousingNeighborhoodResolver>();
        services.AddScoped<HousingProviderResolver>();
        services.AddScoped<HousingGuideResolver>();
        services.AddScoped<HousingTrustVerificationProvider>();

        // The relay: the single translation point where a domain fact becomes a published
        // contract. Registered explicitly rather than scanned because it is a collaborator of the
        // event handlers, not a seam — MediatR finds the handlers, and the handlers ask for this.
        services.AddScoped<HousingIntegrationEventRelay>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new amenity is a new class" literally true. A
    /// module ships an assembly and its accommodation types, amenities, ranking strategies, fraud
    /// providers and disclosure policies appear; this file does not change.
    /// </para>
    /// <para>
    /// <b>Everything scanned is scoped, including the registry.</b> Nearly every real seam holds a
    /// repository or an HTTP client, and a singleton registry over scoped implementations is the
    /// captive dependency the host's provider validation refuses to start on. B4 recorded that
    /// failure first and B6, B7, B8, B9 and B10 each repeated the note; paying one dictionary
    /// construction per request is a small price for not repeating it a seventh time.
    /// </para>
    /// <para>
    /// <b>The search engine is registered last-wins rather than as a collection.</b> There is one
    /// live engine, not a chain: the shipped EF implementation answers from the write model, and a
    /// search module that ships its own registers after this and replaces it wholesale. Nothing
    /// above <see cref="IHousingSearchEngine"/> changes when it does.
    /// </para>
    /// <para>
    /// <b>No availability calendar and no neighbourhood metric provider are registered at all</b>,
    /// and that is the shipped state. Both are seams a later capability lands on: with none
    /// registered the aggregate's own date window is authoritative and the profile is written
    /// entirely by editors, which is honest rather than broken.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IAccommodationTypeContributor>(services, assemblies);
        RegisterAll<IHousingAmenityContributor>(services, assemblies);
        RegisterAll<IHousingRankingStrategy>(services, assemblies);
        RegisterAll<IHousingFraudSignalProvider>(services, assemblies);
        RegisterAll<IAddressDisclosurePolicy>(services, assemblies);
        RegisterAll<INeighborhoodMetricProvider>(services, assemblies);

        services.AddScoped<HousingRankingStrategyRegistry>();

        services.AddScoped<IHousingSearchEngine, EfHousingSearchEngine>();

        // A singleton, and safe as one: it holds a concurrent dictionary and no scoped dependency.
        // It is also the one seam whose whole value is that it outlives a request — a per-request
        // view tracker would de-duplicate nothing.
        services.AddSingleton<IHousingViewTracker, InMemoryHousingViewTracker>();
    }

    /// <summary>
    /// The contracts this module publishes to the rest of the platform.
    /// <para>
    /// <see cref="INeighborhoodDirectory"/> is the one that matters: it is how Jobs, Marketplace,
    /// Providers, Guides and anything shipped later render a neighbourhood without referencing a
    /// Housing aggregate. Registering it here rather than scanning for it is deliberate — there is
    /// exactly one implementation and it is this module's own.
    /// </para>
    /// </summary>
    private static void RegisterIntegration(IServiceCollection services)
    {
        services.AddScoped<INeighborhoodDirectory, NeighborhoodDirectory>();

        // What homes are advertised for, for Geographic Intelligence's cost of living.
        services.AddScoped<IRentStatistics, HousingRentStatistics>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// <para>
    /// One seeder and two sweeps. The commitment sweep is the one that matters: without it, a home
    /// held by somebody who stopped replying never comes back on the market.
    /// </para>
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<HousingCatalogueSeeder>();
        services.AddHostedService<HousingAvailabilitySweep>();
        services.AddHostedService<HousingCommitmentSweep>();
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
