using Diaspora.Modules.Guides.Application.EventHandlers;
using Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Application.Knowledge.EventHandlers;
using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.CountryContext;
using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Drafts;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.KnowledgeAnalytics;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Domain.Recommendations;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.Modules.Guides.Domain.Versions;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.Modules.Guides.Domain.Votes;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Categories;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.Modules.Guides.Infrastructure.Hosting;
using Diaspora.Modules.Guides.Infrastructure.Knowledge;
using Diaspora.Modules.Guides.Infrastructure.Search;
using Diaspora.Modules.Guides.Integration;
using Diaspora.Modules.Guides.Options;
using Diaspora.Modules.Guides.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Guides;

/// <summary>
/// The Guides &amp; Knowledge Platform.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor
/// scan. <b>No edit was needed to the Platform Kernel, <c>Program.cs</c>, Identity, Trust,
/// Location, Media, Community or Providers</b> — the module plugs in, and the modules it consumes
/// do not learn that it exists.
/// </para>
/// <para>
/// <b>Order 700.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300), Media
/// (400), Community (500) and Providers (600). Registration order is not load-bearing — every
/// cross-module dependency here resolves lazily through the container — but ordering last matches
/// the dependency direction: Guides consumes all seven and is consumed by every knowledge surface
/// after it.
/// </para>
/// <para>
/// <b>Four extension seams are assembly-scanned</b>, which is the architectural claim of this
/// module: a category tree, a verification source, a ranking strategy and a search engine are
/// each a class in whichever assembly owns the idea. This file gains no line when one is added.
/// </para>
/// </summary>
public sealed class GuidesModule : IPlatformModule
{
    public string Name => GuidesModuleMetadata.Name;

    public string Version => GuidesModuleMetadata.Version;

    public int Order => 700;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(GuidesModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviors
        // are open generics registered once, so validation, telemetry, transactions,
        // authorisation and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<GuideOptions>()
            .Bind(configuration.GetSection(GuideOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KnowledgeOptions>()
            .Bind(configuration.GetSection(KnowledgeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IntelligenceOptions>()
            .Bind(configuration.GetSection(IntelligenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterEventHandlerCollaborators(services);
        RegisterSeams(services);
        RegisterConsumerContracts(services);
        RegisterHostedServices(services);

        RegisterKnowledgeDirectory(services);

        RegisterCountryIntelligence(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IGuideRepository, GuideRepository>();

        // Cross-module content seams in Diaspora.Contracts: the platform overview's counts and
        // a sponsored item's title. Resolved as collections by Analytics and Creator.
        services.AddScoped<Diaspora.Contracts.Content.V1.IPlatformInventorySource, Persistence.GuidePublishedContentSource>();
        services.AddScoped<Diaspora.Contracts.Content.V1.IContentTitleSource, Persistence.GuidePublishedContentSource>();

        // 21 Oct 2026. An author's published-guide totals, for the creator reputation read.
        services.AddScoped<Diaspora.Contracts.Guides.V1.Authors.IAuthorGuideStatsReader, Persistence.AuthorGuideStatsReader>();

        // 7 Nov 2026. Staff take a guide down only by deciding its Trust case; Trust carries the
        // decision out through this, in the same transaction.
        services.AddScoped<Diaspora.Contracts.Moderation.V1.IModerationTarget, Infrastructure.Moderation.GuideModerationTarget>();
        services.AddScoped<IGuideVersionRepository, GuideVersionRepository>();
        services.AddScoped<IGuideCategoryRepository, GuideCategoryRepository>();
        services.AddScoped<IGuideSuggestionRepository, GuideSuggestionRepository>();
        services.AddScoped<IGuideVoteRepository, GuideVoteRepository>();
        services.AddScoped<IGuideAnalyticsRepository, GuideAnalyticsRepository>();
        services.AddScoped<IGuideAuthorStandingRepository, GuideAuthorStandingRepository>();
        services.AddScoped<IGuideDraftSnapshotRepository, GuideDraftSnapshotRepository>();
    }

    /// <summary>
    /// The application services handlers compose.
    /// <para>
    /// The four cross-module adapters are here rather than in <c>Infrastructure</c> because they
    /// are consumers rather than contributions: the media claimer, the bookmark and discussion
    /// wrappers, the report router and the provider resolver each know exactly one foreign
    /// module, and nothing else in B8 does. That containment is the rule the architecture tests
    /// enforce.
    /// </para>
    /// <para>
    /// Stateless helpers — the snapshot serializer, the reading-time calculator, the diff
    /// service, the wire-name map and every mapper — are static classes and are deliberately
    /// absent: registering a type with no state is a container entry nobody resolves, and
    /// CA1822 in Release is what pushed them static in the first place.
    /// </para>
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<GuideAuthorizationService>();
        services.AddScoped<GuideCategoryResolver>();
        services.AddScoped<GuideSlugAllocator>();
        services.AddScoped<GuideTrendingService>();
        services.AddScoped<GuideAnalyticsRecorder>();
        services.AddScoped<GuideSearchProjectionService>();
        services.AddScoped<GuideScheduledPublicationService>();

        // The cross-module adapters. One class per foreign module, and no other class in B8
        // holds a reference to any of them.
        services.AddScoped<GuideGeographicScopeAdapter>();
        services.AddScoped<GuideMediaClaimer>();
        services.AddScoped<GuideBookmarkService>();
        services.AddScoped<Application.EventHandlers.GuideSavedCount>();
        services.AddScoped<GuideDiscussionService>();
        services.AddScoped<GuideReportService>();
        services.AddScoped<GuideProviderResolver>();
        services.AddScoped<GuideAuthorNames>();
        services.AddScoped<GuideReadComposer>();
    }

    /// <summary>
    /// The relay and the two projectors.
    /// <para>
    /// Registered explicitly rather than scanned because they are collaborators of the event
    /// handlers, not seams: MediatR finds the handlers, and the handlers ask the container for
    /// these. The relay is the single translation point where a domain fact becomes a published
    /// contract; the projectors are the only paths by which Trust's decisions reach a guide's
    /// trust mark or an author's standing.
    /// </para>
    /// </summary>
    private static void RegisterEventHandlerCollaborators(IServiceCollection services)
    {
        services.AddScoped<GuideIntegrationEventRelay>();
        services.AddScoped<GuideTrustVerificationProjector>();
        services.AddScoped<GuideAuthorStandingProjector>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new ranking is a new class" literally true.
    /// A module ships an assembly and its category trees, verification sources and ranking
    /// strategies appear; this file does not change.
    /// </para>
    /// <para>
    /// <b>Everything here is scoped, including the registries.</b> Nearly every real seam holds a
    /// repository or an HTTP client, and a singleton registry over scoped implementations is the
    /// captive dependency the host's provider validation refuses to start on. B4 recorded that
    /// failure with its country metadata catalogue, B6 repeated the note and B7 repeated it
    /// again; paying one dictionary construction per request is a small price for not repeating
    /// it a fourth time.
    /// </para>
    /// <para>
    /// <b>The search engine is registered last-wins rather than as a collection.</b> There is one
    /// live engine, not a chain of them: the shipped EF implementation answers from the write
    /// model, and a search module that ships its own registers after this and replaces it
    /// wholesale. Nothing above <see cref="IGuideSearchEngine"/> changes when it does.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IGuideCategoryContributor>(services, assemblies);
        RegisterAll<IGuideVerificationProvider>(services, assemblies);
        RegisterAll<IGuideRankingStrategy>(services, assemblies);

        services.AddScoped<GuideVerificationProviderRegistry>();
        services.AddScoped<GuideRankingStrategyRegistry>();

        services.AddScoped<IGuideSearchEngine, EfGuideSearchEngine>();
    }

    /// <summary>
    /// The consumer contracts other modules bind to.
    /// <para>
    /// This is the surface B9 and every knowledge-adjacent module reads: resolve a guide, render
    /// its card, browse the catalogue. Nothing else of B8 is public to them, and none of them
    /// needs a guide table of its own.
    /// </para>
    /// <para>
    /// Both are registered by concrete type with the interface forwarded to it, so a query
    /// injecting the service directly and a foreign module resolving the contract share one
    /// instance per scope rather than constructing two.
    /// </para>
    /// </summary>
    private static void RegisterConsumerContracts(IServiceCollection services)
    {
        services.AddScoped<GuideDirectoryService>();
        services.AddScoped<IGuideDirectory>(
            provider => provider.GetRequiredService<GuideDirectoryService>());

        services.AddScoped<GuideCatalogService>();
        services.AddScoped<IGuideCatalog>(
            provider => provider.GetRequiredService<GuideCatalogService>());
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<GuideCategorySeeder>();
        services.AddHostedService<GuideScheduledPublisher>();
    }

    /// <summary>
    /// B8.1 — the Country Knowledge Directory.
    /// <para>
    /// <b>Registered from inside B8's module rather than as a module of its own, and that is the
    /// architectural claim of the phase.</b> The directory curates, organises and presents guides;
    /// the Guides module owns them. One module, one schema, one order, one migration. A second
    /// <c>IPlatformModule</c> would have implied a second owner of guide content, which is precisely
    /// the duplication B8.1 exists to prevent — and it would have needed its own order slot, its own
    /// database contributor and its own answer to who owns a guide.
    /// </para>
    /// <para>
    /// <b>Four seams are assembly-scanned</b>, on the same terms as B8's: a navigation catalogue, a
    /// directory template, a page section and a dashboard context band are each a class in whichever
    /// assembly owns the idea. This method gains no line when one is added.
    /// </para>
    /// <para>
    /// <b>The ranking seam is reused, not redeclared.</b> Ordering within a shelf is B8's
    /// <c>IGuideRankingStrategy</c>; a platform with two answers to "what does most helpful mean"
    /// has no answer.
    /// </para>
    /// </summary>
    private static void RegisterKnowledgeDirectory(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterKnowledgeRepositories(services);
        RegisterKnowledgeServices(services);

        // The seams. Scanned rather than listed, which is what makes "a new shelf is a new class"
        // literally true.
        RegisterAll<IKnowledgeCategoryContributor>(services, assemblies);
        RegisterAll<ICountryDirectoryTemplateContributor>(services, assemblies);
        RegisterAll<IKnowledgeSectionProvider>(services, assemblies);
        RegisterAll<ICountryContextContributor>(services, assemblies);
        RegisterAll<IKnowledgeAnalyticsSink>(services, assemblies);

        // Scoped registries over scoped seams. A singleton registry here is the captive dependency
        // the host's provider validation refuses to start on — B4 recorded it, B6, B7 and B8 each
        // repeated the note, and paying one dictionary construction per request is a small price for
        // not repeating it a fifth time.
        services.AddScoped<KnowledgeSectionProviderRegistry>();
        services.AddScoped<CountryContextContributorRegistry>();

        // Last-wins rather than a collection: there is one live search engine, not a chain of them.
        // Two engines answering one query would have to be merged, and merging two relevance
        // orderings produces a third that is nobody's.
        services.AddScoped<IKnowledgeSearchEngine, EfKnowledgeSearchEngine>();

        // A singleton, and safe as one: a concurrent dictionary and no scoped dependency. It is also
        // the one component whose entire value is outliving a request — a per-request cache caches
        // nothing.
        services.AddSingleton<IKnowledgeCache, InMemoryKnowledgeCache>();

        services.AddScoped<IKnowledgeDirectory>(
            provider => provider.GetRequiredService<KnowledgeDirectoryService>());

        services.AddReferenceDataSeeder<KnowledgeCatalogueSeeder>();
    }

    private static void RegisterKnowledgeRepositories(IServiceCollection services)
    {
        services.AddScoped<IKnowledgeCategoryRepository, KnowledgeCategoryRepository>();
        services.AddScoped<
            ICountryKnowledgeDirectoryRepository, CountryKnowledgeDirectoryRepository>();
        services.AddScoped<ILearningPathRepository, LearningPathRepository>();
        services.AddScoped<
            IKnowledgeRecommendationRepository, KnowledgeRecommendationRepository>();
        services.AddScoped<
            ICountryKnowledgeAnalyticsRepository, CountryKnowledgeAnalyticsRepository>();
        services.AddScoped<IKnowledgeSearchTermRepository, KnowledgeSearchTermRepository>();
        services.AddScoped<ICountryContextSummaryRepository, CountryContextSummaryRepository>();
    }

    /// <summary>
    /// The application services the Knowledge Directory's handlers compose.
    /// <para>
    /// Registered by hand rather than scanned, for the reason B12 records: a repository, a composer
    /// or the relay is a collaborator of this module's own handlers, and scanning for those would let
    /// an accidental second implementation somewhere else silently take over a read path.
    /// </para>
    /// <para>
    /// The mappers and the wire-name map are deliberately absent — they are static classes, and a
    /// container entry for a type with no state is one nobody resolves. CA1822 in Release is what
    /// pushed them static, and B8 records the same lesson.
    /// </para>
    /// </summary>
    private static void RegisterKnowledgeServices(IServiceCollection services)
    {
        services.AddScoped<KnowledgeAuthorizationService>();
        services.AddScoped<KnowledgeGuideFinder>();
        services.AddScoped<KnowledgeCardResolver>();
        services.AddScoped<KnowledgeStatisticsBuilder>();
        services.AddScoped<KnowledgeDirectoryComposer>();
        services.AddScoped<CountryDashboardComposer>();
        services.AddScoped<CountryDirectoryTemplateResolver>();
        services.AddScoped<KnowledgeAnalyticsRecorder>();

        // The projection maintained from other verticals' published contracts, and the relay that
        // is the single translation point where a curation fact becomes a published contract.
        services.AddScoped<CountryContextProjector>();
        services.AddScoped<KnowledgeIntegrationEventRelay>();

        // The consumer contract, registered by concrete type with the interface forwarded to it, so
        // a query injecting the service directly and a foreign module resolving the contract share
        // one instance per scope rather than constructing two.
        services.AddScoped<KnowledgeDirectoryService>();
    }

    /// <summary>
    /// B8.2 — the Country Intelligence Platform.
    /// <para>
    /// <b>Registered from inside B8's module, on exactly the reasoning B8.1 established, and the
    /// brief states the claim outright: country intelligence introduces no new bounded context.</b>
    /// The Guides module already owns what a member needs to know about a country; official notices,
    /// embassy information, visa advisories, emergency contacts and the holiday calendar are the
    /// authoritative half of that same question. One module, one schema, one order, one owner. A
    /// second <c>IPlatformModule</c> would have implied a second owner of country knowledge, needed
    /// its own order slot and its own database contributor, and left the platform with two answers to
    /// what a country page is.
    /// </para>
    /// <para>
    /// <b>Six seams are assembly-scanned</b>: a category catalogue, an emergency vocabulary, a source
    /// policy, a verification provider, a search engine and an analytics sink are each a class in
    /// whichever assembly owns the idea. This method gains no line when one is added.
    /// </para>
    /// <para>
    /// <b>The cache seam is reused, not redeclared</b>, on the same reasoning B8.1 reuses B8's
    /// ranking seam: a deployment configuring Redis should do it once for the module, not once per
    /// phase. The key spaces are distinct so the two invalidate independently.
    /// </para>
    /// </summary>
    private static void RegisterCountryIntelligence(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterIntelligenceRepositories(services);
        RegisterIntelligenceServices(services);

        // The seams. Scanned rather than listed, which is what makes "a new category is a new class"
        // literally true.
        RegisterAll<IOfficialNoticeCategoryContributor>(services, assemblies);
        RegisterAll<IEmergencyContactKindContributor>(services, assemblies);
        RegisterAll<IIntelligenceSourceValidator>(services, assemblies);
        RegisterAll<IAuthorityVerificationProvider>(services, assemblies);
        RegisterAll<IIntelligenceAnalyticsSink>(services, assemblies);

        // Scoped registry over scoped providers. A singleton here is the captive dependency the
        // host's provider validation refuses to start on — B4 recorded it and four phases have
        // repeated the note.
        services.AddScoped<AuthorityVerificationProviderRegistry>();

        // Last-wins rather than a collection: there is one live search engine, not a chain of them.
        services.AddScoped<IOfficialNoticeSearchEngine, EfOfficialNoticeSearchEngine>();

        services.AddReferenceDataSeeder<NoticeCategorySeeder>();
        services.AddHostedService<NoticeExpirySweep>();
    }

    private static void RegisterIntelligenceRepositories(IServiceCollection services)
    {
        services.AddScoped<IOfficialNoticeRepository, OfficialNoticeRepository>();
        services.AddScoped<INoticeCategoryRepository, NoticeCategoryRepository>();
        services.AddScoped<IIssuingAuthorityRepository, IssuingAuthorityRepository>();
        services.AddScoped<IEmbassyRepository, EmbassyRepository>();
        services.AddScoped<
            ICountryEmergencyDirectoryRepository, CountryEmergencyDirectoryRepository>();
        services.AddScoped<IVisaAdvisoryRepository, VisaAdvisoryRepository>();
        services.AddScoped<IPublicHolidayRepository, PublicHolidayRepository>();
        services.AddScoped<
            ICountryIntelligenceAnalyticsRepository, CountryIntelligenceAnalyticsRepository>();
    }

    /// <summary>
    /// The application services the intelligence handlers compose.
    /// <para>
    /// Registered by hand rather than scanned, for the reason B12 records and B8.1 repeats: a
    /// repository, a composer or the relay is a collaborator of this module's own handlers, and
    /// scanning for those would let an accidental second implementation somewhere else silently take
    /// over a read path.
    /// </para>
    /// <para>
    /// The mappers and the wire-name map are deliberately absent — they are static classes, and a
    /// container entry for a type with no state is one nobody resolves. CA1822 in Release is what
    /// pushed them static, and two earlier phases record the same lesson.
    /// </para>
    /// </summary>
    private static void RegisterIntelligenceServices(IServiceCollection services)
    {
        services.AddScoped<IntelligenceAuthorizationService>();
        services.AddScoped<IntelligenceReferenceResolver>();
        services.AddScoped<IntelligenceSourceValidator>();
        services.AddScoped<IntelligenceAnalyticsRecorder>();
        services.AddScoped<OfficialNoticePresenter>();
        services.AddScoped<CountryIntelligenceComposer>();

        // The relay is the single translation point where a domain fact becomes a published
        // contract; the projector is the only path by which a Trust decision reaches a verified
        // badge. Both are collaborators of this module's event handlers rather than seams, so they
        // are named here and MediatR finds the handlers that use them.
        services.AddScoped<IntelligenceIntegrationEventRelay>();
        services.AddScoped<AuthorityVerificationProjector>();
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
