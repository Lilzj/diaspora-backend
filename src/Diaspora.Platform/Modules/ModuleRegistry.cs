using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Modules;

/// <summary>
/// The set of modules making up this host, in the order they configure services.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<ModuleRegistration> _modules = [];

    public IReadOnlyList<ModuleRegistration> Modules =>
        _modules.OrderBy(m => m.Module.Order).ToArray();

    /// <summary>
    /// Registers a module explicitly. Rarely needed now that discovery exists; kept for
    /// tests that want one module and nothing else.
    /// </summary>
    public ModuleRegistry Register<TModule>() where TModule : IPlatformModule, new()
    {
        Add(new TModule());
        return this;
    }

    /// <summary>
    /// Finds every <see cref="IPlatformModule"/> across the platform's own assemblies.
    /// <para>
    /// This is what closes <c>Program.cs</c> to modification: a new module is a new project
    /// reference, not a new line in the composition root.
    /// </para>
    /// </summary>
    public ModuleRegistry DiscoverModules() =>
        DiscoverModules(ModuleAssemblyScanner.FindPlatformAssemblies());

    public ModuleRegistry DiscoverModules(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IPlatformModule>(assemblies))
        {
            if (type.GetConstructor(Type.EmptyTypes) is null)
            {
                throw new InvalidOperationException(
                    $"Module '{type.FullName}' has no parameterless constructor. Modules are " +
                    "instantiated before the container exists, so they cannot take dependencies — " +
                    "resolve what you need inside ConfigureServices instead.");
            }

            Add((IPlatformModule)Activator.CreateInstance(type)!);
        }

        return this;
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        foreach (var registration in Modules)
        {
            registration.Module.ConfigureServices(services, configuration);
        }
    }

    private void Add(IPlatformModule module)
    {
        var existing = _modules.FirstOrDefault(m =>
            string.Equals(m.Name, module.Name, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            // Two modules claiming one name makes the manifest, the permission attribution
            // and the readiness report all ambiguous. Better to stop here than to run with
            // whichever one happened to be scanned first.
            throw new InvalidOperationException(
                $"Two modules are both named '{module.Name}': " +
                $"{existing.Module.GetType().FullName} and {module.GetType().FullName}.");
        }

        _modules.Add(new ModuleRegistration(module.Name, module.Version, module));
    }
}
