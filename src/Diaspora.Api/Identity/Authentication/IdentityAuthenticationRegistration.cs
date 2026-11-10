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
            // the 247 pre-existing exceptions in a dated list that could only shrink.
            //
            // CLOSED 10 November 2026, and this paragraph used to end "explicitly not yet true of
            // that list". The list is empty: all 247 are classified, so that rule now has no
            // exemptions at all, and AuthorizationBehavior REFUSES a request type carrying
            // neither attribute. The compensating control became the guarantee it was described as.
            //
            // The route table is covered too, which it was not: RouteAuthorizationCoverageTests
            // pins the 478 endpoints that declare nothing on the route — every one of them
            // delegating to a request attribute the pipeline now enforces — so an endpoint that
            // loses .RequireAuthorization is named by a failing test rather than quietly served.
            // The health probes are the only two entries on that list reaching no handler.
            //
            // So FallbackPolicy stays null, and for the first time that is a choice rather than a
            // gap: the anonymous endpoints are enumerated, each says why, and nothing reaches a
            // handler without a declaration somewhere.
            options.FallbackPolicy = null;
        });

        // Turns every IPolicyContributor declaration into a registered ASP.NET Core policy.
        services.AddPlatformAuthorizationPolicies();

        return services;
    }
}
