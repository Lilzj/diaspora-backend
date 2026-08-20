using Diaspora.Modules.Integrations.Application.Services;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Extensibility.Http;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Hosting;
using Diaspora.Modules.Integrations.Infrastructure.Http;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Security;
using Diaspora.Modules.Integrations.Options;
using Diaspora.Modules.Integrations.Persistence.Repositories;
using Diaspora.Platform.Modules;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Modules.Integrations;

/// <summary>
/// The Integration Foundation.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, tables, telemetry, secrets and the B13 notification catalogue are all found by the
/// kernel's contributor scan. <c>Program.cs</c>, the Platform Kernel, Identity and Notifications are
/// untouched.
/// </para>
/// <para>
/// <b>Order 2000, last.</b> The number is a dependency direction on this platform — a module may
/// reference one composed before it and never one composed after — and this module reads one:
/// Notifications at 1200, for its catalogue contributor seams. Composing last also means nothing
/// else can depend on this one, which is the correct shape for a boundary layer: a business module
/// that took a reference on the integration foundation would be a business module whose domain logic
/// could reach a provider adapter.
/// </para>
/// <para>
/// <b>Every provider seam is <c>TryAdd</c>, so a vendor adapter replaces the default rather than
/// stacking behind it.</b> An adapter shipped later registers itself before this runs — composition
/// order is what makes that work — and the deployment gets the vendor. Nothing else in the module
/// changes, because nothing else in the module names a vendor.
/// </para>
/// </summary>
public sealed class IntegrationsModule : IPlatformModule
{
    public string Name => IntegrationsModuleMetadata.Name;

    public string Version => IntegrationsModuleMetadata.Version;

    public int Order => 2000;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(IntegrationsModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<IntegrationsOptions>()
            .Bind(configuration.GetSection(IntegrationsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterProviders(services);
        RegisterOutbound(services);
        RegisterHosting(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IIntegrationConnectionRepository, IntegrationConnectionRepository>();
        services.AddScoped<IOAuthAuthorizationRequestRepository, OAuthAuthorizationRequestRepository>();
        services.AddScoped<ICalendarEventLinkRepository, CalendarEventLinkRepository>();
        services.AddScoped<IIntegrationCalendarSettingsRepository, IntegrationCalendarSettingsRepository>();
        services.AddScoped<IIntegrationSyncRunRepository, IntegrationSyncRunRepository>();
        services.AddScoped<IProcessedIntegrationWebhookRepository, ProcessedIntegrationWebhookRepository>();

        services.AddScoped<IntegrationAccessService>();
        services.AddScoped<IntegrationDisconnectService>();

        // The clock, defensively. The host registers one; a module that assumed so and was composed
        // into a bare test container would fail on resolution rather than on a wrong date.
        services.TryAddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Registers the provider boundary and its not-configured defaults.
    /// <para>
    /// <b>The defaults are registered rather than omitted.</b> A missing registration would produce
    /// a resolution failure at the first request; a registered default that answers
    /// <c>NotConfigured</c> produces an answer a client can branch on, and gives the capability
    /// report something to ask. PSR-1.2's rule — "a stub is worse than an absence" — is satisfied
    /// because neither of these pretends the capability exists.
    /// </para>
    /// <para>
    /// <b>There is no not-configured webhook verifier.</b> A callback for a provider with no verifier
    /// is answered 404 and writes no row, and a registered default would have had to choose between
    /// verifying nothing (accepting anything) and refusing everything (a 400 where a 404 is the
    /// honest answer). The registry's null is the third, correct option.
    /// </para>
    /// </summary>
    private static void RegisterProviders(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IOAuthProvider, NotConfiguredOAuthProvider>());

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IExternalCalendarProvider, NotConfiguredCalendarProvider>());

        services.AddScoped<IntegrationProviderRegistry>();

        // Real encryption from the host's own key ring, not a placeholder. See the class.
        services.TryAddScoped<IIntegrationCredentialProtector, DataProtectionCredentialProtector>();

        services.AddSingleton<
            Diaspora.Platform.Operations.Security.ISecretRequirementContributor,
            IntegrationSecretRequirementContributor>();
    }

    /// <summary>
    /// Registers the outbound HTTP boundary.
    /// <para>
    /// A named typed client through <c>IHttpClientFactory</c> — §40 — with no
    /// <c>HttpClient.Timeout</c> set, because the timeout is applied per attempt inside the gateway
    /// so that a hung call cannot consume the retry budget B17's executor is managing. Automatic
    /// redirects are off: a provider redirecting an authenticated call to a host that is not on the
    /// allow-list would carry the bearer token there, and the allow-list check happens before the
    /// call rather than on each hop.
    /// </para>
    /// </summary>
    private static void RegisterOutbound(IServiceCollection services)
    {
        services
            .AddHttpClient(IntegrationHttpGateway.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = false
            });

        services.TryAddScoped<IIntegrationHttpGateway, IntegrationHttpGateway>();
    }

    /// <summary>
    /// Registers the maintenance sweep.
    /// <para>
    /// One <c>BackgroundService</c>, which takes B17's distributed lease before touching anything.
    /// It reaps abandoned synchronisation runs, announces expiring grants, deletes expired
    /// authorization requests and applies the two retention horizons.
    /// </para>
    /// </summary>
    private static void RegisterHosting(IServiceCollection services) =>
        services.AddHostedService<IntegrationMaintenanceWorker>();
}
