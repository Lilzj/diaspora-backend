using Diaspora.Modules.Jobs.Application.EventHandlers;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Domain.Alerts;
using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Applications;
using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Pipelines;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.Resumes;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.Modules.Jobs.Extensibility.Categories;
using Diaspora.Modules.Jobs.Extensibility.Ranking;
using Diaspora.Modules.Jobs.Extensibility.Recommendations;
using Diaspora.Modules.Jobs.Extensibility.Search;
using Diaspora.Modules.Jobs.Infrastructure.Hosting;
using Diaspora.Modules.Jobs.Infrastructure.Search;
using Diaspora.Modules.Jobs.Infrastructure.Trust;
using Diaspora.Modules.Jobs.Options;
using Diaspora.Modules.Jobs.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Jobs;

/// <summary>
/// The Jobs &amp; Careers Platform.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so
/// it lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor
/// scan. <b>No edit was needed to the Platform Kernel, <c>Program.cs</c>, Identity, Trust,
/// Location, Media, Community, Providers or Guides</b> — the module plugs in, and the modules it
/// consumes do not learn that it exists.
/// </para>
/// <para>
/// <b>The single exception is a deletion, not an edit.</b> The host's
/// <c>ProvisionalCatalogContributor</c> has declared <c>jobs.browse</c>, <c>jobs.apply</c> and
/// <c>jobs:apply</c> since B2, against the day this module landed. Those three lines are deleted
/// as B9 lands, which is exactly the one-line-diff shrinkage that file exists to undergo.
/// Leaving them would not be harmless: the feature catalogue refuses two modules declaring one
/// gate, and the startup failure takes down every request that resolves it.
/// </para>
/// <para>
/// <b>Order 800.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300), Media
/// (400), Community (500), Providers (600) and Guides (700). Registration order is not
/// load-bearing — every cross-module dependency resolves lazily through the container — but
/// ordering last matches the dependency direction: Jobs consumes all eight and is consumed by
/// nothing yet.
/// </para>
/// <para>
/// <b>Four extension seams are assembly-scanned</b>, which is the architectural claim of this
/// module: a category tree, a ranking strategy, a recommendation provider and a search engine
/// are each a class in whichever assembly owns the idea. This file gains no line when one is
/// added.
/// </para>
/// </summary>
public sealed class JobsModule : IPlatformModule
{
    public string Name => JobsModuleMetadata.Name;

    public string Version => JobsModuleMetadata.Version;

    public int Order => 800;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(JobsModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline
        // behaviours are open generics registered once, so validation, telemetry, transactions,
        // authorisation and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<JobOptions>()
            .Bind(configuration.GetSection(JobOptions.SectionName))
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
    /// The five cross-module adapters are here rather than in <c>Infrastructure</c> because they
    /// are consumers rather than contributions: the media claimer, the geographic adapter, the
    /// provider resolver and the guide resolver each know exactly one foreign module, and nothing
    /// else in B9 does. That containment is the rule the architecture tests enforce.
    /// </para>
    /// <para>
    /// Stateless helpers — every mapper, the wire-name map, the search projection — are static
    /// classes and are deliberately absent: registering a type with no state is a container entry
    /// nobody resolves, and CA1822 in Release is what pushed them static in the first place.
    /// </para>
    /// </summary>
    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<JobAuthorizationService>();
        services.AddScoped<JobCategoryResolver>();
        services.AddScoped<JobSlugAllocator>();

        // The cross-module adapters. One class per foreign module, and no other class in B9
        // holds a reference to any of them.
        services.AddScoped<JobMediaClaimer>();
        services.AddScoped<JobGeographicScopeAdapter>();
        services.AddScoped<JobAreaFiling>();
        services.AddScoped<JobProviderResolver>();
        services.AddScoped<JobGuideResolver>();
        services.AddScoped<JobTrustVerificationProvider>();

        // The relay: the single translation point where a domain fact becomes a published
        // contract. Registered explicitly rather than scanned because it is a collaborator of
        // the event handlers, not a seam — MediatR finds the handlers, and the handlers ask the
        // container for this.
        services.AddScoped<JobIntegrationEventRelay>();
    }

    /// <summary>
    /// One repository per aggregate. Scoped, like the context they share.
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IEmployerProfileRepository, EmployerProfileRepository>();
        services.AddScoped<ICandidateProfileRepository, CandidateProfileRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<IJobOfferRepository, JobOfferRepository>();
        services.AddScoped<IHiringPipelineRepository, HiringPipelineRepository>();
        services.AddScoped<IResumeRepository, ResumeRepository>();
        services.AddScoped<ISavedJobRepository, SavedJobRepository>();
        services.AddScoped<IJobReportRepository, JobReportRepository>();
        services.AddScoped<IJobAlertRepository, JobAlertRepository>();
        services.AddScoped<IJobCategoryRepository, JobCategoryRepository>();
        services.AddScoped<IJobSkillRepository, JobSkillRepository>();
        services.AddScoped<IJobAnalyticsRepository, JobAnalyticsRepository>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new ranking is a new class" literally true.
    /// A module ships an assembly and its category trees, ranking strategies and recommendation
    /// providers appear; this file does not change.
    /// </para>
    /// <para>
    /// <b>Everything here is scoped, including the registries.</b> Nearly every real seam holds a
    /// repository or an HTTP client, and a singleton registry over scoped implementations is the
    /// captive dependency the host's provider validation refuses to start on. B4 recorded that
    /// failure with its country metadata catalogue, and B6, B7 and B8 each repeated the note;
    /// paying one dictionary construction per request is a small price for not repeating it a
    /// fifth time.
    /// </para>
    /// <para>
    /// <b>The search engine is registered last-wins rather than as a collection.</b> There is one
    /// live engine, not a chain of them: the shipped EF implementation answers from the write
    /// model, and a search module that ships its own registers after this and replaces it
    /// wholesale. Nothing above <see cref="IJobSearchEngine"/> changes when it does.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IJobCategoryContributor>(services, assemblies);
        RegisterAll<IJobRankingStrategy>(services, assemblies);
        RegisterAll<IJobRecommendationProvider>(services, assemblies);

        services.AddScoped<JobRankingStrategyRegistry>();

        // Last-wins, not a collection: there is one live engine. The shipped EF implementation
        // answers from the write model, and a search module that ships its own registers after
        // this and replaces it wholesale. Nothing above IJobSearchEngine changes when it does.
        services.AddScoped<IJobSearchEngine, EfJobSearchEngine>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services) =>
        services.AddReferenceDataSeeder<JobCategorySeeder>();

    private static void RegisterAll<TContract>(
        IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            services.AddScoped(typeof(TContract), type);
        }
    }
}
