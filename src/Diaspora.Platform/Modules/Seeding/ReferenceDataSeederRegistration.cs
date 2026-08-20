using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diaspora.Platform.Modules.Seeding;

/// <summary>
/// Registers a reference-data seeder under both the faces it has to wear.
/// </summary>
public static class ReferenceDataSeederRegistration
{
    /// <summary>
    /// Registers <typeparamref name="TSeeder"/> as a singleton, as the hosted service that runs it
    /// at startup, and as a discoverable <see cref="IReferenceDataSeeder"/> — all three resolving
    /// <b>one instance</b>.
    /// <para>
    /// <b>One line instead of two, because the two could disagree.</b> The alternative a module
    /// would otherwise write — <c>AddHostedService&lt;T&gt;()</c> beside
    /// <c>AddSingleton&lt;IReferenceDataSeeder, T&gt;()</c> — builds <em>two</em> instances of the
    /// same seeder, so a deployment seeds its catalogue twice and the count in the log is wrong in
    /// a way nobody can explain. Registering the concrete type once and forwarding both faces to it
    /// is the pattern ASP.NET Core documents for exactly this shape.
    /// </para>
    /// <para>
    /// <b>The concrete self-registration is what the startup validator reads.</b> A forwarded
    /// descriptor carries a factory rather than an implementation type, so
    /// <c>SeederRegistrationValidator</c> would have nothing to compare against without it.
    /// </para>
    /// </summary>
    public static IServiceCollection AddReferenceDataSeeder<TSeeder>(this IServiceCollection services)
        where TSeeder : class, IReferenceDataSeeder, IHostedService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TSeeder>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<TSeeder>());
        services.AddSingleton<IReferenceDataSeeder>(provider => provider.GetRequiredService<TSeeder>());

        return services;
    }
}
