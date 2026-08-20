using Diaspora.Modules.Financial.Application.Entitlements;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// What the caller is entitled to, and how much of it they have used.
/// <para>
/// <b>One route, self only, and no variant that takes a user identifier.</b> An entitlement snapshot
/// describes what somebody has paid for, which is a financial fact about a named person; a route that
/// accepted an id is a route somebody would eventually pass somebody else's.
/// </para>
/// </summary>
internal static class EntitlementEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/entitlements/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMyEntitlementsQuery(), cancellationToken)))
            .WithName("GetMyEntitlements")
            .WithSummary(
                "Entitlements with their ceilings, and consumption where a vertical reported it. "
                + "A counter with no consumption means 'not measured', never zero.");
    }
}
