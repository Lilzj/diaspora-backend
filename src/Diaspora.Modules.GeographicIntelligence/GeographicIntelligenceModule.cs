using Diaspora.Modules.GeographicIntelligence.Application.Services;
using Diaspora.Modules.GeographicIntelligence.Domain.Routing;
using Diaspora.Modules.GeographicIntelligence.Domain.Safety;
using Diaspora.Modules.GeographicIntelligence.Infrastructure.Routing;
using Diaspora.Modules.GeographicIntelligence.Infrastructure.Safety;
using Diaspora.Modules.GeographicIntelligence.Options;
using Diaspora.Platform.Modules;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>
/// The Geographic Intelligence Platform.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints and telemetry are all found by the kernel's contributor scan. <c>Program.cs</c>, the
/// Platform Kernel, Identity and Trust are untouched.
/// </para>
/// <para>
/// <b>Order 1900, last.</b> The number is a dependency direction on this platform — a module may
/// reference one composed before it and never one composed after — and this module reads two:
/// Location at 300 for the catalogue and its counter registry, Housing at 1000 for the published
/// neighbourhood directory. Composing last is what makes both references legal and the graph
/// acyclic.
/// </para>
/// <para>
/// <b>It registers no repository, no <c>DbContext</c> configuration and no seeder, because it owns
/// no table.</b> Every figure it serves is composed at read time from a module that already holds
/// it. That is the design: a geographic intelligence layer with its own copy of a neighbourhood
/// would be a second answer to where Peckham is.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceModule : IPlatformModule
{
    public string Name => GeographicIntelligenceModuleMetadata.Name;

    public string Version => GeographicIntelligenceModuleMetadata.Version;

    public int Order => 1900;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(GeographicIntelligenceModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<GeographicIntelligenceOptions>()
            .Bind(configuration.GetSection(GeographicIntelligenceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PoliceUkOptions>()
            .Bind(configuration.GetSection(PoliceUkOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterComposition(services);
        RegisterProviders(services);
    }

    private static void RegisterComposition(IServiceCollection services)
    {
        services.AddScoped<NeighborhoodResolver>();
        services.AddScoped<NeighborhoodScoreComposer>();

        // The clock, so a composed `computedAt` and a `validUntil` are testable rather than
        // whatever the machine said. Registered defensively: the host registers one, and a module
        // that assumed so and was composed into a bare test container would fail on resolution
        // rather than on a wrong date.
        services.TryAddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Registers the external provider boundary.
    /// <para>
    /// <b><c>TryAdd</c>, so a vendor adapter replaces this rather than stacking behind it.</b> An
    /// adapter shipped later registers itself before this line runs — composition order is what
    /// makes that work — and the deployment gets the vendor. Nothing else in the module changes,
    /// because nothing else in the module names a vendor.
    /// </para>
    /// <para>
    /// The default answers <c>NotConfigured</c>. It is registered rather than omitted so that the
    /// capability report and the route both have something to ask, and so that "no routing" is a
    /// value a client can read instead of a shape it has to infer from a 404.
    /// </para>
    /// </summary>
    private static void RegisterProviders(IServiceCollection services)
    {
        services.TryAddScoped<IRoutingProvider, NotConfiguredRoutingProvider>();

        // Police-recorded crime for the safety counts. A singleton because it holds the per-point
        // month cache; the named client comes from the factory on every request.
        services.AddHttpClient(PoliceUkCrimeData.HttpClientName, client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Diaspora/1.0 (+https://diaspora-api.fly.dev)"));
        services.TryAddSingleton<IPoliceCrimeData, PoliceUkCrimeData>();
    }
}
