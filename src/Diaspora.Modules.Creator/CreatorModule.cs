using Diaspora.Modules.Creator.Application.Academy;
using Diaspora.Modules.Creator.Application.Challenges;
using Diaspora.Modules.Creator.Application.Consumers;
using Diaspora.Modules.Creator.Application.EventHandlers;
using Diaspora.Modules.Creator.Application.Milestones;
using Diaspora.Modules.Creator.Application.Referrals;
using Diaspora.Modules.Creator.Application.Rewards;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Application.Verification;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Infrastructure.Hosting;
using Diaspora.Modules.Creator.Options;
using Diaspora.Modules.Creator.Persistence;
using Diaspora.Modules.Creator.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Diaspora.Modules.Creator.Application.Earnings;

namespace Diaspora.Modules.Creator;

/// <summary>
/// The Creator Platform's registration.
/// <para>
/// <b>Order 1700, after everything.</b> The number is load-bearing: this module reads B15 (1400) for
/// the points seam, and the seam registries are built in module order, so B15 must be composed
/// before this one resolves <c>IRewardPointsAccount</c>. Nothing composes after it, which makes the
/// module a leaf in the graph — no module depends on the Creator Platform, and the architecture test
/// that forbids a backward reference has nothing to check here beyond that.
/// </para>
/// <para>
/// <b>Everything is registered by hand, and there is one contributor interface that is not
/// declared.</b> This module ships no seam of its own — no ranking provider, no search projector, no
/// content source — because there is nothing another module would plug into. Declaring an empty one
/// so it would look like its siblings is the <c>IEligibilityService</c> mistake PSR-1.2 recorded: a
/// stub is worse than an absence. The extension points it does have are the published contracts it
/// consumes; adding a tenth kind of recognised action is a constant, a consumer and a seeded rule.
/// </para>
/// <para>
/// <b>What it does not register is as much the point as what it does.</b> No points balance, no
/// wallet, no ledger, no notification service, no scheduler, no idempotency store, no fraud engine
/// and no analytics sink. Each of those exists on this platform already or belongs to a phase that
/// has not run; an architecture test asserts this assembly names neither SignalR, nor a push vendor,
/// nor a payment gateway.
/// </para>
/// </summary>
public sealed class CreatorModule : IPlatformModule
{
    public string Name => CreatorModuleMetadata.Name;

    public string Version => CreatorModuleMetadata.Version;

    /// <summary>See the class summary: this must be greater than B15's 1400 and B18's 1600.</summary>
    public int Order => 1700;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(CreatorModule).Assembly;

        // Handlers, consumers and validators from this assembly. The Application layer's pipeline
        // behaviours are open generics registered once, so validation, telemetry, transactions,
        // authorisation and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<CreatorOptions>()
            .Bind(configuration.GetSection(CreatorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<CreatorRevenueShareOptions>()
            .Bind(configuration.GetSection(CreatorRevenueShareOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ICreatorProfileRepository, CreatorProfileRepository>();
        services.AddScoped<ICreatorVerificationRepository, CreatorVerificationRepository>();
        services.AddScoped<ICreatorMilestoneRepository, CreatorMilestoneRepository>();
        services.AddScoped<ICreatorMilestoneProgressRepository, CreatorMilestoneProgressRepository>();
        services.AddScoped<ICreatorChallengeRepository, CreatorChallengeRepository>();
        services.AddScoped<
            ICreatorChallengeEnrollmentRepository, CreatorChallengeEnrollmentRepository>();
        services.AddScoped<ICommunityGrantRepository, CommunityGrantRepository>();
        services.AddScoped<IGrantApplicationRepository, GrantApplicationRepository>();
        services.AddScoped<IAcademyRepository, AcademyRepository>();
        services.AddScoped<ICreatorLearningProgressRepository, CreatorLearningProgressRepository>();
        services.AddScoped<ICreatorCertificateRepository, CreatorCertificateRepository>();
        services.AddScoped<ICreatorReferralRepository, CreatorReferralRepository>();
        services.AddScoped<ISignupBonusRepository, SignupBonusRepository>();
        services.AddScoped<ICreatorRewardRepository, CreatorRewardRepository>();
        services.AddScoped<ICreatorActivityRepository, CreatorActivityRepository>();
        services.AddScoped<ICreatorEarningRepository, CreatorEarningRepository>();

        // A narrow read across the ledger rather than a repository operation on an aggregate — see
        // the type's own summary for why it is not a method on ICreatorActivityRepository.
        services.AddScoped<CreatorActivityLookup>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // The one door to "who is calling, and what may they do to their own creator record". Every
        // write in the module goes through it, so the answer is one answer rather than eleven.
        services.AddScoped<CreatorContext>();

        // The only caller of CreatorStatusPolicy.Resolve outside its own tests. A handler that set a
        // status directly would be a second place the ladder is decided.
        services.AddScoped<CreatorStatusService>();

        // B15's idempotency primitive, applied to creator writes. Not a second mechanism — see the
        // class's own documentation, and §24 of the phase brief.
        services.AddScoped<CreatorIdempotency>();

        services.AddScoped<CreatorBadgeReader>();
        services.AddScoped<CreatorVerificationRequirements>();
        services.AddScoped<SignupBonusEligibility>();

        // The three progress services. Extracted from their consumers because DSP1001 forbids a
        // handler reaching another handler, and because milestones, challenges and course completion
        // are each needed by more than one caller.
        services.AddScoped<MilestoneProgressService>();
        services.AddScoped<ChallengeProgressService>();
        services.AddScoped<CourseCompletionService>();

        // Referral issuing and qualification in one place, because they are the same rule seen from
        // two ends — splitting them is how a self-referral gets through.
        services.AddScoped<ReferralQualificationService>();

        // The single settling path behind nine published contracts. Registered explicitly rather
        // than scanned because it is a collaborator of the consumers, not a seam.
        services.AddScoped<CreatorActivityRecorder>();

        // The single settling path for an earning. Both money consumers call into it, so the rate
        // arithmetic, the idempotency check and the creator lookup exist once.
        services.AddScoped<CreatorEarningRecorder>();

        // Resolves a guide's author from this module's own ledger rather than referencing B8 for a
        // Guid. Registered by hand so a second implementation cannot appear by accident.
        services.AddScoped<IGuidesAuthorLookup, ActivityLedgerAuthorLookup>();

        // The single translation point where a domain fact becomes a published contract.
        services.AddScoped<CreatorIntegrationEventRelay>();

        // ── B23.x Wave 12: what a creator may automate, and what their standing is ──────────────
        //
        // Wave 11 shipped four creator triggers and a follower broadcast, all working, all gated on
        // `automations.creator` — which the automation platform's own floor grants to nobody, with the
        // stated reason that it "cannot tell a verified creator from anybody else; that is B18's
        // knowledge". These two are B18 saying it.
        //
        // Registered here rather than in the automation module, and possible only because the seams
        // were moved to Diaspora.Contracts in Wave 12: this module composes at 1400 and the automation
        // platform at 2100, so a reference to its assembly would be the forward dependency the
        // platform's own guardrail forbids.
        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationScopeAuthority,
            Extensibility.Automation.CreatorAutomationAuthority>();

        services.AddScoped<
            Diaspora.Contracts.Automation.V1.Extensibility.IAutomationConditionSource,
            Extensibility.Automation.CreatorAutomationConditionSource>();
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        // Discovered rather than listed, since PSR-1: the integration harness resolves whatever is
        // registered and runs it in module order, so this catalogue arrives there with no edit.
        services.AddReferenceDataSeeder<CreatorCatalogueSeeder>();

        // Takes B17's IDistributedLock before touching anything, and has a configuration switch —
        // DEBT-049's lesson, applied on the module's first day rather than four phases in.
        // The sweep resolves this per pass. Scoped, because it writes through the same change
        // tracker the pass's unit of work saves.
        services.AddScoped<CreatorEarningClearing>();

        services.AddHostedService<CreatorLifecycleSweep>();
    }
}
