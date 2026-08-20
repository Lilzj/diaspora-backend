using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Validation;

/// <summary>
/// Everything a validator is allowed to look at.
/// <para>
/// <b>The registration snapshot is the interesting half.</b> "Is this contributor registered
/// exactly once?" and "is this seeder registered at all?" cannot be answered from an
/// <see cref="IServiceProvider"/>: resolving a service tells you what you got, not how many
/// descriptors produced it, and asking for a type nobody registered gives you null rather than a
/// diagnosis. The <see cref="IServiceCollection"/> is gone by the time anything runs, so a copy of
/// its descriptors is taken while it still exists.
/// </para>
/// </summary>
public sealed class StartupValidationContext
{
    public StartupValidationContext(
        IServiceProvider services,
        IReadOnlyList<ServiceDescriptor> registrations,
        IReadOnlyList<Assembly> assemblies,
        PlatformValidationOptions? options = null)
    {
        Services = services;
        Registrations = registrations;
        Assemblies = assemblies;
        Options = options ?? new PlatformValidationOptions();
    }

    /// <summary>A scoped provider. Resolving anything scoped is safe.</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// The settings this run was started with.
    /// <para>
    /// Carried on the context rather than injected, so that a validator honours the options the
    /// <em>runner</em> was given. A validator resolving <c>IOptions</c> itself would read the
    /// configured value and ignore the caller's — which made
    /// <c>Run(new PlatformValidationOptions { ValidateModuleCatalogs = false })</c> a no-op, and made
    /// the test asserting that behaviour pass without exercising it.
    /// </para>
    /// </summary>
    public PlatformValidationOptions Options { get; }

    /// <summary>Every service descriptor the host was built from.</summary>
    public IReadOnlyList<ServiceDescriptor> Registrations { get; }

    /// <summary>The platform assemblies, found the way the kernel finds them.</summary>
    public IReadOnlyList<Assembly> Assemblies { get; }

    /// <summary>
    /// How many descriptors would produce <paramref name="implementation"/> for
    /// <paramref name="service"/>. Zero means unregistered; more than one means registered twice,
    /// which for an additive seam means every contribution counted twice.
    /// </summary>
    public int CountRegistrations(Type service, Type implementation) =>
        Registrations.Count(descriptor =>
            descriptor.ServiceType == service && descriptor.ImplementationType == implementation);

    /// <summary>Whether anything at all is registered for <paramref name="service"/>.</summary>
    public bool IsRegistered(Type service) =>
        Registrations.Any(descriptor => descriptor.ServiceType == service);
}
