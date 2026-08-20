using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Diaspora.Platform.Modules;

/// <summary>
/// Finds the assemblies that may contain modules and contributors.
/// <para>
/// Resolved from the dependency context rather than <c>AppDomain.GetAssemblies()</c>,
/// which only reports what the runtime has already been asked to load. A module whose
/// types nothing has touched yet — the normal case at startup — would simply not be there.
/// </para>
/// <para>
/// Scoped to assemblies whose name starts with <c>Diaspora.</c>. Scanning the whole
/// dependency graph would mean reflecting over every framework and third-party assembly
/// on every boot, for types that by definition cannot be there.
/// </para>
/// </summary>
public static class ModuleAssemblyScanner
{
    private const string AssemblyNamePrefix = "Diaspora.";

    public static IReadOnlyList<Assembly> FindPlatformAssemblies()
    {
        var loaded = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(IsPlatformAssembly)
            .ToDictionary(assembly => assembly.GetName().Name!, StringComparer.Ordinal);

        var context = DependencyContext.Default;
        if (context is not null)
        {
            foreach (var library in context.RuntimeLibraries)
            {
                if (!library.Name.StartsWith(AssemblyNamePrefix, StringComparison.Ordinal) ||
                    IsTestAssembly(library.Name) ||
                    loaded.ContainsKey(library.Name))
                {
                    continue;
                }

                try
                {
                    var assembly = Assembly.Load(new AssemblyName(library.Name));
                    loaded[library.Name] = assembly;
                }
                catch (Exception exception) when (
                    exception is FileNotFoundException or FileLoadException or BadImageFormatException)
                {
                    // A dependency-context entry with no loadable assembly beside it is a
                    // packaging problem, not a module. Skipping keeps a stray reference
                    // from taking down the host.
                }
            }
        }

        return loaded.Values
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Concrete, public, parameterless-constructible implementations of
    /// <typeparamref name="TContract"/> across the supplied assemblies.
    /// </summary>
    public static IReadOnlyList<Type> FindImplementations<TContract>(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
                typeof(TContract).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Instantiates every implementation of <typeparamref name="TContract"/> found in the
    /// supplied assemblies.
    /// <para>
    /// For the handful of contributions the host needs before the container exists — the
    /// telemetry source names, which OpenTelemetry wants at registration time.
    /// </para>
    /// </summary>
    public static IReadOnlyList<TContract> CreateAll<TContract>(IEnumerable<Assembly> assemblies)
        where TContract : class
    {
        return FindImplementations<TContract>(assemblies)
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null)
            .Select(type => (TContract)Activator.CreateInstance(type)!)
            .ToArray();
    }

    private static bool IsPlatformAssembly(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return !assembly.IsDynamic &&
               name?.StartsWith(AssemblyNamePrefix, StringComparison.Ordinal) == true &&
               !IsTestAssembly(name);
    }

    /// <summary>
    /// Excludes test assemblies from module and contributor discovery.
    /// <para>
    /// A <c>WebApplicationFactory</c> host boots the real <c>Program.cs</c> inside the test
    /// assembly, and that assembly — along with anything it references, such as a shared
    /// test-helpers project — is in the dependency context. A test fixture that implements a
    /// contributor interface would otherwise be discovered and registered into the host,
    /// which is how the integration suite first surfaced a phantom "TestContributorModule"
    /// colliding with a real one. Nothing named <c>*Tests</c> ships to production, so
    /// excluding the suffix costs nothing real.
    /// </para>
    /// </summary>
    private static bool IsTestAssembly(string assemblyName) =>
        assemblyName.EndsWith("Tests", StringComparison.Ordinal);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // One unloadable type must not hide every module in the assembly.
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
