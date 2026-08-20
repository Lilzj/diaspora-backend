using Diaspora.Modules.Housing.Application.Dashboards;
using Diaspora.Modules.Housing.Application.Discovery;
using Diaspora.Modules.Housing.Application.Listings;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// Finding somewhere to live: search and one property.
/// <para>
/// <b>Both routes are anonymous, and that is a product decision rather than an oversight.</b>
/// Somebody deciding whether they can afford to move to a country needs to see what is available
/// and what it costs before they join anything. What they see is redacted by the same rules that
/// apply to everybody else — no address without a confirmed viewing, no contact details unless the
/// landlord published them — so openness costs nothing and closing it would make the module useful
/// only to people who had already decided.
/// </para>
/// </summary>
internal static class HousingDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/dashboard", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetHousingDashboardQuery(), cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("GetHousingDashboard")
            .WithSummary("What a landlord sees when they open the platform.");

        group.MapGet("/search", async (
                [AsParameters] SearchHousingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchHousingQuery { Request = request }, cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchHousing")
            .WithSummary("Search for somewhere to live.");

        group.MapGet("/listings/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetHousingListingQuery { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetHousingListing")
            .WithSummary("One property, with as much of it as the caller may see.");
    }
}
