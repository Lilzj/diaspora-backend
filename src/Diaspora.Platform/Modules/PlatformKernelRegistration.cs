using System.Reflection;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Authorization.AspNetCore;
using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Scalability;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Platform.Modules;

/// <summary>
/// Boots the platform kernel: discovers modules, collects their contributions, and
/// composes the permission, feature and policy catalogues.
/// <para>
/// One call in <c>Program.cs</c>, and it never needs another. Adding Trust, Marketplace or
/// Messaging is a project reference — the module declares what it owns through the
/// contributor interfaces and the kernel finds it. That is the whole of B2.6's
/// extensibility work in one method.
/// </para>
/// </summary>
public static class PlatformKernelRegistration
{
    public static IServiceCollection AddPlatformKernel(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var scanned = (assemblies ?? ModuleAssemblyScanner.FindPlatformAssemblies()).ToArray();

        var registry = new ModuleRegistry().DiscoverModules(scanned);
        services.AddSingleton(registry);
        services.AddSingleton(new PlatformManifest(registry));

        // Bound here rather than beside the startup validator that reads it, because how many
        // instances the deployment runs is not a fact about validation — the dashboard, a cache key
        // prefix and anything else that needs to know the topology reads the same options. Bound in
        // the kernel means every host has it, including the ones that switch validation off.
        services.AddOptions<DeploymentOptions>()
            .Bind(configuration.GetSection(DeploymentOptions.SectionName));

        RegisterContributors<IPermissionContributor>(services, scanned);
        RegisterContributors<IFeatureContributor>(services, scanned);
        RegisterContributors<IPolicyContributor>(services, scanned);
        RegisterContributors<IObservabilityContributor>(services, scanned);
        RegisterContributors<IEndpointContributor>(services, scanned);

        // B17. Operational policies compose exactly like the authorisation catalogues, and are
        // discovered from the same assembly scan, so a module that wants a stricter breaker for its
        // own external dependency declares one without the host learning its name.
        RegisterContributors<IOperationalPolicyContributor>(services, scanned);

        services.AddSingleton<IPermissionCatalog, PermissionCatalog>();
        services.AddSingleton<IFeatureCatalog, FeatureCatalog>();
        services.AddSingleton<IPolicyCatalog, PolicyCatalog>();
        services.AddSingleton<IOperationalPolicyCatalog, OperationalPolicyCatalog>();

        // Composed from whatever the modules register below. Built through a factory rather than
        // by constructor injection so the two optional dependencies are read as "use it if the
        // host has one" — a unit-test host with no module registry still gets a working runner,
        // seeding in name order instead of module order.
        services.AddSingleton(provider => new ReferenceDataSeedRunner(
            provider.GetServices<IReferenceDataSeeder>(),
            provider.GetService<ModuleRegistry>(),
            provider.GetService<ILogger<ReferenceDataSeedRunner>>()));

        // Replaced by whichever module owns identity. Left in place it denies everything,
        // which is the right way for an authorisation component to fail.
        services.TryAddScoped<IAuthorizationSubjectAccessor, AnonymousAuthorizationSubjectAccessor>();

        // Scoped, not singleton. Both handlers depend on IAuthorizationSubjectAccessor,
        // which is scoped because it reads the current request's claims — a singleton
        // handler capturing it is the classic captive-dependency bug, and the container's
        // scope validation catches it at startup. The catalogues they also depend on are
        // singletons, which a scoped service may consume freely.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AccessTierAuthorizationHandler>();

        // Modules configure services after the kernel, so they can replace kernel defaults
        // (as Identity does for the subject accessor) and depend on the catalogues.
        registry.ConfigureServices(services, configuration);

        return services;
    }

    /// <summary>
    /// Turns every declared <see cref="Contracts.Authorization.PolicyDefinition"/> into a
    /// registered ASP.NET Core policy, once under its canonical name and once under each alias
    /// it carries.
    /// <para>
    /// Deferred through <c>AddOptions</c> rather than run inline: the policy catalogue is a
    /// service, and resolving it during registration would mean building a second container
    /// and losing every module registration that came after.
    /// </para>
    /// <para>
    /// <b>An alias is built from the same definition, not copied from the registered policy.</b>
    /// Two registrations sharing one builder body cannot drift: a requirement added to the
    /// canonical name is on the alias in the same edit, which is the only version of "both names
    /// authorise identically" that survives the next change to authorisation.
    /// </para>
    /// </summary>
    public static IServiceCollection AddPlatformAuthorizationPolicies(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AuthorizationOptions>()
            .Configure<IPolicyCatalog>((options, catalog) =>
            {
                foreach (var policy in catalog.All)
                {
                    options.AddPolicy(policy.Name, builder => Build(builder, policy));

                    foreach (var alias in policy.Aliases)
                    {
                        options.AddPolicy(alias, builder => Build(builder, policy));
                    }
                }
            });

        return services;
    }

    private static void Build(
        AuthorizationPolicyBuilder builder, Contracts.Authorization.PolicyDefinition policy)
    {
        builder.RequireAuthenticatedUser();

        if (policy.Permission is { } permission)
        {
            builder.AddRequirements(new PermissionRequirement(permission));
        }

        if (policy.MinimumTier is { } tier)
        {
            builder.AddRequirements(new AccessTierRequirement(tier, policy.RequiresExactTier));
        }
    }

    private static void RegisterContributors<TContributor>(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies)
        where TContributor : class
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContributor>(assemblies))
        {
            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Contributor '{type.FullName}' has no parameterless constructor. Contributors " +
                    "declare static metadata and are built while the container is still being " +
                    "assembled, so they cannot take dependencies.");
            }

            services.AddSingleton(typeof(TContributor), type);
        }
    }
}
