using Diaspora.Api.Health;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Middleware;

/// <summary>
/// Host-header filtering for every request but the orchestrator's probes (BSEC-012).
/// <para>
/// ASP.NET Core filters hosts already: every host it builds runs <see cref="HostFilteringMiddleware"/>
/// ahead of all other middleware, with the <c>AllowedHosts</c> list. That position is the problem.
/// Fly.io's documentation says its service check reaches the machine over the app's private network
/// rather than through the public name, and does not say what Host it sends; its users report the
/// machine's own address, <c>172.x.x.x:8080</c>. A check answered 400 is a failed check, and Fly
/// stops routing to a machine whose check fails until it passes again. This deployment has one
/// machine, so the framework's own filter, set to a real list, would take the whole API down at the
/// first check.
/// </para>
/// <para>
/// So the framework's filter is told to accept any host, and the configured list is applied here,
/// with the same middleware, to every path but <see cref="PlatformHealthChecks.LivePath"/> and
/// <see cref="PlatformHealthChecks.ReadyPath"/>. The probes build no URL from the request and tell a
/// stranger nothing (BSEC-009), so they can answer any Host. Rate limiting exempts the same two paths,
/// for the same caller.
/// </para>
/// </summary>
public static class AllowedHostFiltering
{
    /// <summary>Binds and validates <see cref="AllowedHostOptions"/>, and stands the framework's own filter down.</summary>
    public static IServiceCollection AddAllowedHostFiltering(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AllowedHostOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.Hosts = AllowedHostOptions.Parse(configuration[AllowedHostOptions.ConfigurationKey]))
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AllowedHostOptions>, AllowedHostOptionsValidator>();

        // The framework fills its options from AllowedHosts only when nothing has set them first.
        // Set here, its filter accepts every host, and the list is applied once, by the filter
        // below, where the probes can be let through.
        services.Configure<HostFilteringOptions>(options => options.AllowedHosts = ["*"]);

        return services;
    }

    /// <summary>
    /// Refuses, with a bare 400, a request whose Host is not on the configured list, unless it is one
    /// of the two probes. Adds nothing where the list accepts any host.
    /// </summary>
    public static IApplicationBuilder UseAllowedHostFiltering(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var configured = app.ApplicationServices.GetRequiredService<IOptions<AllowedHostOptions>>().Value;

        if (configured.AcceptsAnyHost)
        {
            return app;
        }

        var filtering = new FixedOptions(new HostFilteringOptions
        {
            AllowedHosts = [.. configured.Hosts],

            // A request with no Host at all can only be an HTTP/1.0 client reaching the machine
            // directly. Fly's proxy always sends one.
            AllowEmptyHosts = false,

            // A bare 400. The framework's message is a page of HTML, and this host serves no page it
            // did not write (BSEC-010).
            IncludeFailureMessage = false,
        });

        return app.UseWhen(
            context => !IsProbe(context.Request.Path),
            branch => branch.UseMiddleware<HostFilteringMiddleware>(filtering));
    }

    /// <summary>The two probe paths, matched as routing matches them: the whole path, in any case.</summary>
    internal static bool IsProbe(PathString path) =>
        path.Equals(PlatformHealthChecks.LivePath, StringComparison.OrdinalIgnoreCase)
        || path.Equals(PlatformHealthChecks.ReadyPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>One fixed set of options for the filter above, apart from the framework's own.</summary>
    private sealed class FixedOptions(HostFilteringOptions options) : IOptionsMonitor<HostFilteringOptions>
    {
        public HostFilteringOptions CurrentValue => options;

        public HostFilteringOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<HostFilteringOptions, string?> listener) => null;
    }
}
