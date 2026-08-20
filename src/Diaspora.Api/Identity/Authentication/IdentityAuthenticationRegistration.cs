using Diaspora.Platform.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diaspora.Api.Identity.Authentication;

/// <summary>
/// Registers JWT bearer authentication and hands policy registration to the kernel.
/// <para>
/// No configuration is read here. The bearer options are supplied by
/// <see cref="JwtBearerOptionsConfigurator"/> through the options system, so they observe
/// every configuration source the host ends up with rather than only those present when
/// this method ran.
/// </para>
/// <para>
/// B2 built Identity's twelve policies inline in this method. They now come from the
/// policy catalogue, which every module contributes to — so this file no longer changes
/// when a module adds a policy, and the requirement types it used to construct live in the
/// kernel where other modules can reach them.
/// </para>
/// </summary>
public static class IdentityAuthenticationRegistration
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsConfigurator>());

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddAuthorization(options =>
        {
            // No fallback policy: several identity endpoints are deliberately anonymous
            // (register, sign-in, refresh, recovery). An architecture test is what stops a
            // new endpoint from being accidentally public — a check that names the offending
            // action beats a runtime default that has to be opted out of correctly eleven
            // times.
            options.FallbackPolicy = null;
        });

        // Turns every IPolicyContributor declaration into a registered ASP.NET Core policy.
        services.AddPlatformAuthorizationPolicies();

        return services;
    }
}
