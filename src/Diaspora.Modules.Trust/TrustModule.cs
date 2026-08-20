using Diaspora.Contracts.Trust.V1.Submission;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Services.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Modules.Trust;

/// <summary>
/// The Trust &amp; Verification Engine.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly
/// so it lands in the build output and names it nowhere else; permissions, feature gates,
/// policies, endpoints, telemetry and database configuration are all found by the kernel's
/// contributor scan. <c>Program.cs</c>, the Platform Kernel and the Identity module are
/// untouched.
/// </para>
/// <para>
/// <b>Order 200.</b> After Identity (1) because the module's endpoints authorise against
/// tiers Identity's subject accessor supplies, and after Bookmarks (100) only by convention —
/// the two do not interact. The ordering that matters is that this runs after
/// <c>AddInfrastructure</c>, which is guaranteed: the kernel calls module
/// <c>ConfigureServices</c> last, which is what makes the <c>ITrustService</c> takeover below
/// possible without a kernel edit.
/// </para>
/// </summary>
public sealed class TrustModule : IPlatformModule
{
    public string Name => TrustModuleMetadata.Name;

    public string Version => TrustModuleMetadata.Version;

    public int Order => 200;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(TrustModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's own scan ran
        // before this module existed to the container, but the pipeline behaviors it
        // registered are open generics — so validation, telemetry, transactions and
        // authorisation apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        RegisterRepositories(services);
        RegisterExtensibility(services, assembly);
        RegisterServices(services, configuration);
        RegisterConsumerContracts(services);

        TakeOverPlatformTrustService(services);
        TakeOverPlatformEligibilityService(services);
    }

    /// <summary>
    /// The contracts other modules bind to.
    /// <para>
    /// One, and it is the one B7 needed and could not have: a way into the verification workflow
    /// that is not an HTTP call to this module's own controller. The interface lives in
    /// <c>Diaspora.Contracts</c> so a consumer can submit without referencing Trust — the same
    /// property that lets it consume Trust's published events — and B7's architecture test still
    /// asserts it holds no dependency on this assembly.
    /// </para>
    /// <para>
    /// Scoped, because the workflow writes through the request's unit of work. A singleton over
    /// it would be the captive dependency the host's provider validation refuses to start on.
    /// </para>
    /// </summary>
    private static void RegisterConsumerContracts(IServiceCollection services) =>
        services.AddScoped<ITrustVerificationSubmission, TrustVerificationSubmissionService>();

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IVerificationRequestRepository, VerificationRequestRepository>();
        services.AddScoped<ITrustProfileRepository, TrustProfileRepository>();
        services.AddScoped<IReputationProfileRepository, ReputationProfileRepository>();
        services.AddScoped<IAppealRepository, AppealRepository>();
        services.AddScoped<ITrustEventRepository, TrustEventRepository>();
        services.AddScoped<IModerationItemRepository, ModerationItemRepository>();
        services.AddScoped<ICommunityContributionRepository, CommunityContributionRepository>();
    }

    /// <summary>
    /// Discovers the module's extension points from every loaded <c>Diaspora.*</c> assembly.
    /// <para>
    /// Scanning rather than listing is what makes the brief's "future verification types,
    /// badges, trust rules and reputation calculators can be added without modifying existing
    /// code" literally true. A Marketplace module shipping a <c>TrustedSeller</c> badge rule
    /// is discovered here without this file changing.
    /// </para>
    /// <para>
    /// The catalogues are singletons because they are immutable compositions built once, and
    /// because each validates its own uniqueness at construction — a duplicate verification
    /// type or a re-priced reputation action fails the boot rather than resolving by load
    /// order.
    /// </para>
    /// </summary>
    private static void RegisterExtensibility(IServiceCollection services, System.Reflection.Assembly assembly)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterImplementations<IVerificationTypeHandler>(services, assemblies);
        RegisterImplementations<IBadgeRule>(services, assemblies);
        RegisterImplementations<ITrustRule>(services, assemblies);
        RegisterImplementations<IReputationRuleContributor>(services, assemblies);
        RegisterImplementations<ITrustRequirementContributor>(services, assemblies);
        RegisterImplementations<IReputationCalculator>(services, assemblies);

        services.AddSingleton<VerificationTypeHandlerRegistry>();
        services.AddSingleton<ReputationRuleCatalog>();
        services.AddSingleton<TrustRequirementCatalog>();
        services.AddSingleton<BadgeRuleCatalog>();
        services.AddSingleton<TrustRuleChain>();
    }

    private static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TrustProfileProvisioner>();
        services.AddScoped<ReputationService>();

        // B3.0.2. The workflow, and the two entry points that share it — the member's own
        // route through MediatR, and the published in-process contract below.
        services.AddScoped<VerificationSubmissionCoordinator>();
        services.AddScoped<BadgeEvaluator>();
        services.AddScoped<EligibilityEvaluator>();

        // Takes IStorageService optionally — see DocumentLinkFactory on why. Trust does not
        // register a storage implementation of its own; the brief is explicit that storage is
        // the platform's to provide, and inventing one here would hide the fact that it has
        // not been provided yet.
        services.AddScoped<DocumentLinkFactory>();

        // The ADR-002 channel back to Identity. An HTTP call by design, so that extracting
        // this module into its own service later is a configuration change rather than a
        // rewrite.
        services.AddOptions<TrustDecisionOptions>()
            .Bind(configuration.GetSection(TrustDecisionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(HttpTrustDecisionPublisher.HttpClientName);
        services.AddScoped<ITrustDecisionPublisher, HttpTrustDecisionPublisher>();
    }

    /// <summary>
    /// Replaces the kernel's placeholder <c>ITrustService</c> with this module's real one.
    /// <para>
    /// This is the line ADR-002 was written for. The kernel registers
    /// <c>DefaultTrustService</c>, which answers <c>Basic</c> and <c>true</c> to everything —
    /// a deliberately, visibly wrong placeholder. B3 owns the contract, and takes it over
    /// here.
    /// </para>
    /// <para>
    /// <c>Replace</c> rather than <c>TryAdd</c>: the kernel's registration already exists by
    /// the time modules configure, and the kernel explicitly sanctions this ("modules
    /// configure services after the kernel, so they can replace kernel defaults"). Identity
    /// does the same for <c>IFeatureGateService</c>. Nothing in the kernel changes.
    /// </para>
    /// </summary>
    private static void TakeOverPlatformTrustService(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Scoped<ITrustService, TrustEngineService>());

    /// <summary>
    /// Replaces the kernel's placeholder <c>IEligibilityService</c> with this module's real one.
    /// <para>
    /// The same line as above, for the port that was left behind when it was written. B3 took over
    /// <c>ITrustService</c> because ADR-002 named it; nothing named this one, and
    /// <c>DefaultEligibilityService</c> — which returns <c>Eligible</c> for every action, for every
    /// member — has been the platform's answer to "may this member do X" ever since, while this
    /// module computed the real one and served it over HTTP. No consumer exists, so nothing changes
    /// today; the point is that the next one is answered by the module that knows.
    /// </para>
    /// </summary>
    private static void TakeOverPlatformEligibilityService(IServiceCollection services) =>
        services.Replace(ServiceDescriptor.Scoped<IEligibilityService, TrustEligibilityService>());

    private static void RegisterImplementations<TContract>(
        IServiceCollection services, System.Reflection.Assembly[] assemblies)
        where TContract : class
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            services.AddSingleton(typeof(TContract), type);
        }
    }
}
