using Diaspora.Modules.Marketplace.Application.EventHandlers;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Modules.Marketplace.Domain.Analytics;
using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Promotions;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Attributes;
using Diaspora.Modules.Marketplace.Extensibility.Categories;
using Diaspora.Modules.Marketplace.Extensibility.Fraud;
using Diaspora.Modules.Marketplace.Extensibility.Ranking;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.Modules.Marketplace.Extensibility.Views;
using Diaspora.Modules.Marketplace.Infrastructure.Hosting;
using Diaspora.Modules.Marketplace.Infrastructure.Search;
using Diaspora.Modules.Marketplace.Infrastructure.Trust;
using Diaspora.Modules.Marketplace.Infrastructure.Views;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Modules.Marketplace.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Marketplace;

/// <summary>
/// The Marketplace &amp; Commerce Platform.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor scan.
/// <b>No edit was needed to the Platform Kernel, <c>Program.cs</c>, Identity, Trust, Location,
/// Media, Community, Providers, Guides or Jobs</b> — the module plugs in, and the modules it
/// consumes do not learn that it exists.
/// </para>
/// <para>
/// <b>The single exception is a deletion, not an edit.</b> The host's
/// <c>ProvisionalCatalogContributor</c> has declared <c>marketplace.browse</c> and
/// <c>marketplace.listings.create</c> since B2, against the day this module landed. Those two
/// entries are deleted as B10 lands, which is exactly the one-line-diff shrinkage that file exists
/// to undergo. Leaving them would not be harmless: the feature catalogue refuses two modules
/// declaring one gate, and the startup failure takes down every request that resolves it.
/// </para>
/// <para>
/// <b>Order 900.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300), Media (400),
/// Community (500), Providers (600), Guides (700) and Jobs (800). Registration order is not
/// load-bearing — every cross-module dependency resolves lazily through the container — but
/// ordering last matches the dependency direction: Marketplace consumes six of them and is consumed
/// by nothing yet.
/// </para>
/// <para>
/// <b>Seven extension seams are assembly-scanned</b>, which is the architectural claim of this
/// module: a category tree, a category's attribute set, a ranking strategy, a fraud signal source,
/// a search engine, a messaging provider and a view tracker are each a class in whichever assembly
/// owns the idea. This file gains no line when one is added.
/// </para>
/// </summary>
public sealed class MarketplaceModule : IPlatformModule
{
    public string Name => MarketplaceModuleMetadata.Name;

    public string Version => MarketplaceModuleMetadata.Version;

    public int Order => 900;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(MarketplaceModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        // Validated at startup rather than on first read. A mistyped value in a module section used
        // to surface wherever the option was first touched — for several of these, that is a
        // background sweep at three in the morning rather than a request anybody was watching.
        services.AddOptions<MarketplaceOptions>()
            .Bind(configuration.GetSection(MarketplaceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterHostedServices(services);
    }

    /// <summary>
    /// The application services handlers compose.
    /// <para>
    /// The five cross-module adapters are here rather than in <c>Infrastructure</c> because they are
    /// consumers rather than contributions: the media claimer, the geographic adapter, the provider
    /// resolver, the guide resolver and the Trust submission provider each know exactly one foreign
    /// module, and nothing else in B10 does. That containment is the rule the architecture tests
    /// enforce.
    /// </para>
    /// <para>
    /// Stateless helpers — every mapper, the wire-name map, the search projection — are static
    /// classes and are deliberately absent: registering a type with no state is a container entry
    /// nobody resolves, and CA1822 in Release is what pushed them static in the first place.
    /// </para>
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<MarketplaceAuthorizationService>();

        // Cross-module content seams in Diaspora.Contracts: the platform overview's counts and
        // a sponsored item's title. Resolved as collections by Analytics and Creator.
        services.AddScoped<Diaspora.Contracts.Content.V1.IPlatformInventorySource, Persistence.ListingPublishedContentSource>();
        services.AddScoped<Diaspora.Contracts.Content.V1.IContentTitleSource, Persistence.ListingPublishedContentSource>();

        // How a Trust moderation case carries its decision out on a listing (7 Nov 2026).
        services.AddScoped<Diaspora.Contracts.Moderation.V1.IModerationTarget, Infrastructure.Moderation.MarketplaceListingModerationTarget>();
        services.AddScoped<SellerReviewSummaryService>();
        services.AddScoped<ListingCategoryResolver>();
        services.AddScoped<ListingAttributeValidator>();
        services.AddScoped<DonationCauseLink>();
        services.AddScoped<ListingSlugAllocator>();
        services.AddScoped<ListingFraudAssessor>();
        services.AddScoped<ConversationBroker>();

        // Shared by the two Trust consumers. A service rather than a method on one of them,
        // because MediatR registers a handler as INotificationHandler<T> and not as its own type —
        // a handler injecting another handler fails provider validation at startup and takes every
        // other module down with it.
        services.AddScoped<TrustOutcomeApplier>();

        // The cross-module adapters. One class per foreign module, and no other class in B10 holds
        // a reference to any of them.
        services.AddScoped<MarketplaceMediaClaimer>();
        services.AddScoped<MarketplaceGeographicScopeAdapter>();
        services.AddScoped<ListingAreaFiling>();
        services.AddScoped<MarketplaceProviderResolver>();
        services.AddScoped<MarketplaceGuideResolver>();
        services.AddScoped<MarketplaceTrustVerificationProvider>();

        // The relay: the single translation point where a domain fact becomes a published contract.
        // Registered explicitly rather than scanned because it is a collaborator of the event
        // handlers, not a seam — MediatR finds the handlers, and the handlers ask for this.
        services.AddScoped<MarketplaceIntegrationEventRelay>();
    }

    /// <summary>One repository per aggregate. Scoped, like the context they share.</summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IMarketplaceListingRepository, MarketplaceListingRepository>();
        services.AddScoped<ISellerProfileRepository, SellerProfileRepository>();
        services.AddScoped<IListingOfferRepository, ListingOfferRepository>();
        services.AddScoped<IListingReservationRepository, ListingReservationRepository>();
        services.AddScoped<ISellerReviewRepository, SellerReviewRepository>();

        // The stock guard. Declared in the domain, satisfied in persistence over the platform's
        // guarded atomic reservation store — so the module depends on its own abstraction and the
        // concurrency mechanism behind it belongs to B1.3.
        services.AddScoped<IListingStockGuard, EfListingStockGuard>();
        services.AddScoped<IListingCategoryRepository, ListingCategoryRepository>();
        services.AddScoped<
            IListingAttributeDefinitionRepository, ListingAttributeDefinitionRepository>();
        services.AddScoped<ISavedListingRepository, SavedListingRepository>();
        services.AddScoped<IListingReportRepository, ListingReportRepository>();
        services.AddScoped<IListingPromotionRepository, ListingPromotionRepository>();
        services.AddScoped<IListingConversationRepository, ListingConversationRepository>();
        services.AddScoped<IListingAnalyticsRepository, ListingAnalyticsRepository>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new ranking is a new class" literally true. A
    /// module ships an assembly and its category trees, attribute sets, ranking strategies and fraud
    /// providers appear; this file does not change.
    /// </para>
    /// <para>
    /// <b>Everything scanned is scoped, including the registry.</b> Nearly every real seam holds a
    /// repository or an HTTP client, and a singleton registry over scoped implementations is the
    /// captive dependency the host's provider validation refuses to start on. B4 recorded that
    /// failure with its country metadata catalogue, and B6, B7, B8 and B9 each repeated the note;
    /// paying one dictionary construction per request is a small price for not repeating it a sixth
    /// time.
    /// </para>
    /// <para>
    /// <b>The search engine and the view tracker are registered last-wins rather than as
    /// collections.</b> There is one live engine and one live tracker, not a chain of them: the
    /// shipped EF implementation answers from the write model, and a search module that ships its
    /// own registers after this and replaces it wholesale. Nothing above
    /// <see cref="IMarketplaceSearchEngine"/> changes when it does.
    /// </para>
    /// <para>
    /// <b>No conversation provider is registered here at all</b>, and that is the shipped state: on
    /// a deployment with no Communication module the broker's optional dependency stays null, a
    /// conversation reference remains requested, and the client falls back to the seller's contact
    /// preference. The Communication module registers one and B10 does not change.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IListingCategoryContributor>(services, assemblies);
        RegisterAll<IListingAttributeContributor>(services, assemblies);
        RegisterAll<IListingRankingStrategy>(services, assemblies);
        RegisterAll<IListingFraudSignalProvider>(services, assemblies);

        services.AddScoped<ListingRankingStrategyRegistry>();

        services.AddScoped<IMarketplaceSearchEngine, EfMarketplaceSearchEngine>();

        // A singleton, and safe as one: it holds a concurrent dictionary and the clock, and no
        // scoped dependency. It is also the one seam whose whole value is that it outlives a
        // request — a per-request view tracker would de-duplicate nothing.
        services.AddSingleton<IListingViewTracker, InMemoryListingViewTracker>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// <para>
    /// One seeder and three sweeps. The commitment sweep is the one that matters: without it, stock
    /// held by buyers who never turned up never returns to the market.
    /// </para>
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<MarketplaceCatalogueSeeder>();
        services.AddHostedService<ListingExpirySweep>();
        services.AddHostedService<CommitmentExpirySweep>();
        services.AddHostedService<PromotionExpirySweep>();
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
