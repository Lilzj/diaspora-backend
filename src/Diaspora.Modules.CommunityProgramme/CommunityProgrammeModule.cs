using Diaspora.Modules.CommunityProgramme.Application.EventHandlers;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Financial.Extensibility.Advertising;
using Diaspora.Modules.CommunityProgramme.Infrastructure.Advertising;
using Diaspora.Modules.CommunityProgramme.Infrastructure.Hosting;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Modules.CommunityProgramme.Options;
using Diaspora.Modules.CommunityProgramme.Persistence.Repositories;
using Diaspora.Platform.Modules;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// B18's registration.
/// <para>
/// <b>Order 1600, after everything.</b> The number is load-bearing for two reasons. This module reads
/// B12 (1100) for the organization and its role edge, and B15 (1400) for the currency catalogue, and
/// the seam registries are built in module order — so both must be composed before this one scans.
/// And nothing composes after it, which is what makes the module a leaf in the graph: no module
/// depends on B18, and the architecture test that forbids a backward reference has nothing to check
/// here beyond that.
/// </para>
/// <para>
/// <b>Everything is registered by hand, and there are no seams to scan.</b> That is unusual for this
/// platform and it is deliberate. B18 has no contributor interface of its own because it has no
/// reference data, no ranking, no search engine to swap and no catalogue for another module to extend
/// — a membership tier is an organization's data rather than a deployment's. Declaring an empty seam
/// so the module would look like its siblings is the <c>IEligibilityService</c> mistake PSR-1.2
/// recorded: a stub is worse than an absence.
/// </para>
/// <para>
/// <b>What it does not register is as much the point as what it does.</b> No notification service, no
/// scheduler, no idempotency store, no currency table, no organization repository. Each of those
/// exists on this platform already, and an architecture test asserts this module names neither
/// SignalR, nor a push vendor, nor a payment gateway.
/// </para>
/// </summary>
public sealed class CommunityProgrammeModule : IPlatformModule
{
    public string Name => CommunityProgrammeModuleMetadata.Name;

    public string Version => CommunityProgrammeModuleMetadata.Version;

    /// <summary>See the class summary: this must be greater than Community Life's 1100 and B15's 1400.</summary>
    public int Order => 1600;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(CommunityProgrammeModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours are
        // open generics registered once, so validation, telemetry, transactions, authorisation and
        // audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<CommunityProgrammeOptions>()
            .Bind(configuration.GetSection(CommunityProgrammeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        // The community aggregate and standing in one. Registered before the programme
        // repositories because a community is what a programme's organization serves.
        services.AddScoped<ICommunityRepository, CommunityRepository>();
        services.AddScoped<ICommunityMembershipRepository, CommunityMembershipRepository>();
        services.AddScoped<ICommunityDeletionRequestRepository, CommunityDeletionRequestRepository>();
        services.AddScoped<ICommunityOwnershipTransferRepository, CommunityOwnershipTransferRepository>();

        services.AddScoped<IMembershipProgrammeRepository, MembershipProgrammeRepository>();
        services.AddScoped<IProgrammeMembershipRepository, ProgrammeMembershipRepository>();
        services.AddScoped<IVolunteerOpportunityRepository, VolunteerOpportunityRepository>();
        services.AddScoped<IVolunteerApplicationRepository, VolunteerApplicationRepository>();
        services.AddScoped<IVolunteerAssignmentRepository, VolunteerAssignmentRepository>();
        services.AddScoped<ICommunityPollRepository, CommunityPollRepository>();
        services.AddScoped<ICommunityAnnouncementRepository, CommunityAnnouncementRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // The one door to an organization-scoped act. Every write in the module goes through it, so
        // "may this caller do this here" has one answer rather than nine.
        services.AddScoped<CommunityProgrammeAccessService>();
        services.AddScoped<Application.Communities.CommunityModerationAccess>();

        services.AddScoped<MembershipEligibilityEvaluator>();
        services.AddScoped<VolunteerEligibilityService>();
        services.AddScoped<PollAudienceResolver>();

        // The tally and the visibility rule, shared by the results query and the vote command. A
        // service rather than one handler dispatching the other's query — DSP1001, and the reason it
        // exists: a handler that can reach any other handler can reach one whose authorization it has
        // not performed.
        services.AddScoped<PollResultReader>();

        // B15's idempotency primitive, plus the concurrent-duplicate resolution B15's own handlers
        // leave to a 500. Not a second mechanism — see the class's own documentation.
        services.AddScoped<ProgrammeIdempotency>();

        // ── B15.1's two seams into this module ───────────────────────────────────────────────
        //
        // B15 references no vertical, so it asks rather than looks: who holds a membership it is
        // taking money for, and who may run an organization's fundraising. Both answers are this
        // module's, and both are registered here rather than discovered by B15's assembly scanner —
        // this module composes after B15 (1600 against 1400), so AddScoped's last-wins behaviour
        // replaces B15's refusing defaults. Registering them by hand is what makes that ordering a
        // decision rather than an accident of how assemblies happen to sort.
        services.AddScoped<
            Modules.Financial.Extensibility.Memberships.IMembershipSubjectAuthority,
            Application.Financial.MembershipSubjectAuthority>();

        services.AddScoped<
            Modules.Financial.Extensibility.Campaigns.ICampaignOrganizerAuthority,
            Application.Financial.CampaignOrganizerAuthority>();

        // The single translation point where a domain fact becomes a published contract. Registered
        // explicitly rather than scanned because it is a collaborator of the event handlers, not a
        // seam — MediatR finds the handlers, and the handlers ask for this.
        services.AddScoped<CommunityProgrammeIntegrationEventRelay>();

        // 11 Sep 2026. This module's answer for the Community owner kind, which until now nobody
        // gave: the floor answers None() for it, so every community-owned automation part was
        // reachable by nobody. Registered here rather than in the automation module because the seam
        // lives in Diaspora.Contracts for exactly this reason — the module that knows who runs a
        // community is the one that says so.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationScopeAuthority,
            Extensibility.Automation.CommunityAutomationAuthority>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        // Both take B17's IDistributedLock before touching anything, and both have a configuration
        // switch — DEBT-049's lesson, applied on the module's first day rather than four phases in.
        services.AddHostedService<CommunityProgrammeLifecycleSweep>();
        services.AddHostedService<CommunityProgrammeReconciliationSweep>();

        // Not AddHostedService: AddReferenceDataSeeder registers the one instance three ways, so
        // the integration factory can await the seed deterministically instead of racing the host.
        // Registering it as a plain hosted service builds a second instance that nothing can await.
        services.AddReferenceDataSeeder<CommunityDirectorySeeder>();

        // B16 asks; B18 answers, because B18 owns the vocabulary. Financial references no other
        // module, so a targeting picker reaches real communities only through a contribution like
        // this one. See IAdTargetingOptionProvider.
        services.AddScoped<IAdTargetingOptionProvider, CommunityTargetingOptionProvider>();
        services.AddScoped<IAdAudienceSizer, CommunityAudienceSizer>();
    }
}
