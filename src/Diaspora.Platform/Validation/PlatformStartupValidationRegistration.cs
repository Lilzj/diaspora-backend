using System.Reflection;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Validation.Validators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Diaspora.Platform.Validation;

/// <summary>
/// Wires the boot-time check in.
/// <para>
/// <b>Called last, and that is not a style choice.</b> The snapshot is the service collection as it
/// finally stands, so anything registered after this call is invisible to every validator — a
/// seeder registered later would be reported as missing, a contributor registered later as
/// unregistered. One line at the bottom of <c>Program.cs</c>, after the modules, after messaging,
/// after authentication.
/// </para>
/// <para>
/// <b>Validators are discovered, not listed.</b> The seven the kernel ships are found the same way a
/// module's would be, so a vertical that wants its own invariant checked at boot writes a class and
/// changes nothing else — which is the whole of B2.6's extension model applied to validation.
/// </para>
/// </summary>
public static class PlatformStartupValidationRegistration
{
    public static IServiceCollection AddPlatformStartupValidation(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var scanned = (assemblies ?? ModuleAssemblyScanner.FindPlatformAssemblies()).ToArray();

        services.AddOptions<PlatformValidationOptions>()
            .Bind(configuration.GetSection(PlatformValidationOptions.SectionName));

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IStartupValidator>(scanned))
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IStartupValidator), type));
        }

        // Taken here, after everything above it in Program.cs has registered. Adding the snapshot
        // itself changes the collection, which is why it is built from a copy rather than held by
        // reference — a live reference would include the descriptors added below it and read
        // differently depending on when a validator looked.
        services.AddSingleton(new ServiceRegistrationSnapshot(services, scanned));

        // First among hosted services. Fail fast means before the outbox dispatcher opens a
        // connection and before six sweeps and a seeder each start a timer against a platform the
        // host has not yet checked.
        services.Insert(
            0,
            ServiceDescriptor.Singleton<IHostedService, PlatformStartupValidationService>());

        // Also resolvable on its own, so the integration suite can run the report and read it
        // rather than infer it from whether a host started.
        services.AddSingleton<PlatformStartupValidationService>();

        return services;
    }

    /// <summary>
    /// The validators the kernel ships, named so the architecture suite can assert the set is
    /// complete rather than assert that some validators exist.
    /// </summary>
    public static IReadOnlyList<Type> KernelValidators { get; } =
    [
        typeof(ModuleMetadataValidator),
        typeof(ContributorRegistrationValidator),
        typeof(AuthorizationCatalogValidator),
        typeof(FeatureGateValidator),
        typeof(SeederRegistrationValidator),
        typeof(ModuleCatalogValidator),
        typeof(DeploymentTopologyValidator)
    ];
}
