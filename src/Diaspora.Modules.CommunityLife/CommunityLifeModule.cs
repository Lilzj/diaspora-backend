using Diaspora.Modules.CommunityLife.Application.EventHandlers;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Extensibility.Calendar;
using Diaspora.Modules.CommunityLife.Extensibility.Catalog;
using Diaspora.Modules.CommunityLife.Extensibility.CheckIn;
using Diaspora.Modules.CommunityLife.Extensibility.Membership;
using Diaspora.Modules.CommunityLife.Extensibility.Moderation;
using Diaspora.Modules.CommunityLife.Extensibility.Ranking;
using Diaspora.Modules.CommunityLife.Extensibility.Search;
using Diaspora.Modules.CommunityLife.Extensibility.Views;
using Diaspora.Modules.CommunityLife.Extensibility.Visibility;
using Diaspora.Modules.CommunityLife.Infrastructure.Calendar;
using Diaspora.Modules.CommunityLife.Infrastructure.Hosting;
using Diaspora.Modules.CommunityLife.Infrastructure.Integration;
using Diaspora.Modules.CommunityLife.Infrastructure.Search;
using Diaspora.Modules.CommunityLife.Infrastructure.Views;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Modules.CommunityLife.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.CommunityLife;

/// <summary>
/// B12's registration.
/// <para>
/// <b>Order 1100, after Housing.</b> An event can name a neighbourhood B11 profiles, so B11 has to
/// be able to answer before B12 asks — and the seam registries are built in module order.
/// </para>
/// <para>
/// <b>The seams are scanned; everything else is registered by hand.</b> A contributor is a seam and
/// arrives from whatever assembly implements it. A repository, a resolver or the relay is a
/// collaborator of this module's own handlers, and scanning for those would make an accidental
/// second implementation somewhere else silently take over a write path.
/// </para>
/// </summary>
public sealed class CommunityLifeModule : IPlatformModule
{
    public string Name => CommunityLifeModuleMetadata.Name;

    public string Version => CommunityLifeModuleMetadata.Version;

    public int Order => 1100;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(CommunityLifeModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<CommunityLifeOptions>()
            .Bind(configuration.GetSection(CommunityLifeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterIntegration(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ICommunityEventRepository, CommunityEventRepository>();
        services.AddScoped<IEventSeriesRepository, EventSeriesRepository>();
        services.AddScoped<IOrganizationMembershipRepository, OrganizationMembershipRepository>();
        services.AddScoped<IEventRsvpRepository, EventRsvpRepository>();
        services.AddScoped<IOrganizationTypeRepository, OrganizationTypeRepository>();
        services.AddScoped<IEventCategoryRepository, EventCategoryRepository>();
        services.AddScoped<ICommunityLifeReportRepository, CommunityLifeReportRepository>();
        services.AddScoped<IEventAnalyticsRepository, EventAnalyticsRepository>();
        services.AddScoped<IOrganizationAnalyticsRepository, OrganizationAnalyticsRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<CommunityLifeAuthorizationService>();
        services.AddScoped<CommunityLifeCatalogResolver>();
        services.AddScoped<CommunityLifeSlugAllocator>();
        services.AddScoped<CommunityLifeSpamAssessor>();
        services.AddScoped<CommunityLifeMembershipPolicyEvaluator>();
        services.AddScoped<CommunityLifeCheckInDispatcher>();
        services.AddScoped<OrganizationReadContext>();
        services.AddScoped<CommunityEventReadContext>();
        services.AddScoped<EventSeriesReadContext>();

        // B23.x Wave 12. What an organization's people may automate, and who a broadcast reaches.
        //
        // Wave 11 shipped `notify.organization` working and granted to nobody, because the automation
        // platform's own floor "cannot tell a moderator from a member — that is B6, B12 and B18's
        // knowledge". This module holds the membership and the roles, so it answers. Registered here
        // and not there: this module composes at 1100 and the automation platform at 2100, and a
        // reference to its assembly would be the forward dependency the platform forbids. The seam
        // lives in Diaspora.Contracts for exactly that reason.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationScopeAuthority,
            Extensibility.Automation.OrganizationAutomationAuthority>();

        // 20 Oct 2026. Who may publish a Community post as an organisation: an Organizer or above.
        // Community composes at 500 and cannot reference this module, so it asks every registered
        // IPublisherAuthority; Community Programme registers the other one, for communities.
        services.AddScoped<
            Diaspora.Contracts.Community.V1.Publishing.IPublisherAuthority,
            Extensibility.Publishing.OrganizationPublisherAuthority>();

        // Phase 5. What an event answers when a member is building or testing a rule about it. The
        // automation platform had no way to describe a subject, so `POST /automations/simulate` took
        // a sample event's identifier, carried it into an otherwise empty trigger context, and told
        // every member that the event trigger does not carry a country. It does — the consumer three
        // directories away puts it there. This is the same values, from the same module, behind the
        // same visibility gate the event endpoint uses.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationSubjectSource,
            Extensibility.Automation.EventAutomationSubjectSource>();

        // B12.1. The single place an occurrence is ever written — creation, top-up and regeneration
        // after a rule change all come through it, so there is one answer to what an occurrence is
        // stamped with.
        services.AddScoped<EventSeriesExpansionService>();

        // The cross-module adapters. One class per foreign module, and no other class in B12 holds
        // a reference to any of them.
        services.AddScoped<CommunityLifeMediaClaimer>();
        services.AddScoped<CommunityLifeNeighborhoodResolver>();
        services.AddScoped<CommunityLifeGeographicScopeAdapter>();
        services.AddScoped<CommunityLifeProviderResolver>();
        services.AddScoped<CommunityLifeGuideResolver>();
        services.AddScoped<CommunityLifeTrustProvider>();

        // The relay: the single translation point where a domain fact becomes a published contract.
        // Registered explicitly rather than scanned because it is a collaborator of the event
        // handlers, not a seam — MediatR finds the handlers, and the handlers ask for this.
        services.AddScoped<CommunityLifeIntegrationEventRelay>();
    }

    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IOrganizationTypeContributor>(services, assemblies);
        RegisterAll<IEventCategoryContributor>(services, assemblies);
        RegisterAll<IEventRankingStrategy>(services, assemblies);
        RegisterAll<IEventSpamSignalProvider>(services, assemblies);
        RegisterAll<IOrganizationMembershipPolicyProvider>(services, assemblies);
        RegisterAll<IEventCheckInMethod>(services, assemblies);
        RegisterAll<IEventVisibilityPolicy>(services, assemblies);
        RegisterAll<ICommunityCalendarSource>(services, assemblies);

        services.AddScoped<EventRankingStrategyRegistry>();

        services.AddScoped<ICommunityEventSearchEngine, EfCommunityEventSearchEngine>();
        services.AddScoped<IOrganizationSearchEngine, EfOrganizationSearchEngine>();

        // A singleton, and safe as one: it holds a concurrent dictionary and no scoped dependency.
        // It is also the one seam whose whole value is that it outlives a request — a per-request
        // view tracker would de-duplicate nothing.
        services.AddSingleton<ICommunityLifeViewTracker, InMemoryCommunityLifeViewTracker>();
    }

    private static void RegisterIntegration(IServiceCollection services)
    {
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();
        services.AddScoped<IEventDirectory, EventDirectory>();

        // B18. The one write another module may perform against B12's role edge, and it performs it
        // by dispatching B12's own command — see IOrganizationRoleAdministrator for why this is a
        // port rather than a method on the directory.
        services.AddScoped<IOrganizationRoleAdministrator, OrganizationRoleAdministrator>();

        // 21 Oct 2026. What a community's members organised, counted, for its statistics in
        // Community Programme. See IMemberEventActivityReader.
        services.AddScoped<
            Diaspora.Contracts.CommunityLife.V1.Activity.IMemberEventActivityReader,
            MemberEventActivityReader>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<CommunityLifeCatalogueSeeder>();
        services.AddHostedService<CommunityLifeLifecycleSweep>();
        services.AddHostedService<CommunityLifeMembershipSweep>();
        services.AddHostedService<CommunityLifeSeriesExpansionSweep>();
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
