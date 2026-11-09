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
            // (register, sign-in, refresh, recovery).
            //
            // The sentence that used to follow this one said "an architecture test is what stops a
            // new endpoint from being accidentally public". That test did not exist. It was
            // described here, planned, and never written, and the only rule occupying its name
            // asserted that more than a hundred endpoints were authorised — which is true, green,
            // and says nothing about any particular endpoint. BSEC-003 is what the gap cost:
            // GET /api/v1/creators/me/activity served GetMyCreatorActivityQuery to unauthenticated
            // callers for as long as the route existed.
            //
            // RequestAuthorizationArchitectureTests is the rule, written on 9 November 2026. It
            // requires every query and command to carry [Authorize] or [AllowAnonymous], holding
            // the 247 pre-existing exceptions in a dated list that can only shrink. So the claim is
            // now true of everything written from today, and explicitly not yet true of that list.
            //
            // It is still a compensating control rather than a guarantee: it reasons about CQRS
            // request types, not the composed route table, so an endpoint that reaches no handler
            // is outside it. AuthorizationBehavior becomes fail-closed when the list is empty,
            // which is the remaining half of the finding.
            options.FallbackPolicy = null;
        });

        // Turns every IPolicyContributor declaration into a registered ASP.NET Core policy.
        services.AddPlatformAuthorizationPolicies();

        return services;
    }
}
