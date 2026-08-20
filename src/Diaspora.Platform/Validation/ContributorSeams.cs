using System.Reflection;

namespace Diaspora.Platform.Validation;

/// <summary>
/// Finds the platform's contributor seams without being told what they are.
/// <para>
/// <b>Discovered rather than listed, for the reason the architecture suite already records.</b>
/// The first model-reading test in this repository hard-coded seven database contributors and was
/// still reading seven two verticals later, which exempted Marketplace and Housing from a platform
/// rule by omission and silently. A list of seams here would rot the same way: the kernel ships
/// six, the verticals declare two dozen more, and the next module will declare another.
/// </para>
/// <para>
/// The convention is the one every seam in the platform already follows — an interface, named
/// <c>I…Contributor</c>, under a <c>Diaspora.</c> namespace. Nothing else in the codebase is named
/// that way, and a seam that broke the convention would be worth renaming rather than worth
/// special-casing.
/// </para>
/// </summary>
public static class ContributorSeams
{
    /// <summary>
    /// The seams the kernel builds itself, before the container exists. These are the ones whose
    /// implementations must be parameterless, and the ones whose absence is an error rather than a
    /// module's own business.
    /// </summary>
    public static IReadOnlySet<string> KernelConstructed { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "IPermissionContributor",
            "IFeatureContributor",
            "IPolicyContributor",
            "IObservabilityContributor",
            "IEndpointContributor",
            "IDatabaseContributor",

            // B17. The operational policy catalogue is composed at the same moment as the
            // authorisation catalogues and for the same reason: the resilience executor, the
            // limiter and the cache all read it on their first call, which can be before the first
            // request. A policy contributor that needed the container would be a policy nobody
            // could enforce during startup.
            "IOperationalPolicyContributor"
        };

    /// <summary>Every contributor interface across the platform's assemblies.</summary>
    public static IReadOnlyList<Type> Find(IEnumerable<Assembly> assemblies) =>
        [.. Types(assemblies)
            .Where(type =>
                type.IsInterface &&
                type.IsPublic &&
                type.Name.StartsWith('I') &&
                type.Name.EndsWith("Contributor", StringComparison.Ordinal) &&
                type.Namespace?.StartsWith("Diaspora.", StringComparison.Ordinal) == true)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];

    /// <summary>Concrete, instantiable implementations of <paramref name="seam"/>.</summary>
    public static IReadOnlyList<Type> Implementations(Type seam, IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(seam);

        return [.. Types(assemblies)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false } &&
                seam.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The owning module a contributor declares, when it declares one. Read by convention because
    /// the seams do not share a base interface — every one of them declares <c>Module</c>
    /// separately, which is a small duplication the kernel chose over a marker interface every
    /// module would have to know about.
    /// </summary>
    public static string? DeclaredModule(object contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        var property = contributor.GetType().GetProperty(
            "Module", BindingFlags.Public | BindingFlags.Instance);

        return property?.PropertyType == typeof(string)
            ? property.GetValue(contributor) as string
            : null;
    }

    private static IEnumerable<Type> Types(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies)
        {
            Type?[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                // One unloadable type must not hide every seam in the assembly.
                types = exception.Types;
            }

            foreach (var type in types)
            {
                if (type is not null)
                {
                    yield return type;
                }
            }
        }
    }
}
