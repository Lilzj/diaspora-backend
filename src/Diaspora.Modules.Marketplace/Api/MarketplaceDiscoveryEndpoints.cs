using Diaspora.Modules.Marketplace.Application.Dashboards;
using Diaspora.Modules.Marketplace.Application.Discovery;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// Finding things to buy.
/// <para>
/// <b>Every route here is anonymous, and that is a product decision rather than an oversight.</b>
/// Somebody deciding whether this platform is worth joining has to be able to see what is for sale
/// on it. Visibility is applied inside each handler, so a draft or a hidden listing resolves as
/// not-found to a stranger and as itself to its seller.
/// </para>
/// </summary>
internal static class MarketplaceDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/", async (
                [FromQuery] string? country,
                [FromQuery] string? city,
                [FromQuery] Guid? areaId,
                [FromQuery] double? latitude,
                [FromQuery] double? longitude,
                [FromQuery] double? radiusKm,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMarketplaceDashboardQuery
                        {
                            CountryCode = country,
                            City = city,
                            AreaId = areaId,
                            Latitude = latitude,
                            Longitude = longitude,
                            RadiusKm = radiusKm
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetMarketplaceHome")
            .WithSummary("The marketplace home surface: categories, featured, recent and nearby.");

        group.MapPost("/search", async (
                [FromBody] SearchListingsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchListingsQuery { Request = request }, cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchMarketplaceListings")
            .WithSummary("Search and browse listings.");

        group.MapGet("/listings/{listingId:guid}", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetListingQuery
                        {
                            ListingId = listingId,
                            ViewerKey = ViewerKey(context)
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetMarketplaceListing")
            .WithSummary("One listing in full.");

        group.MapGet("/listings/by-slug/{slug}", async (
                string slug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetListingQuery { Slug = slug, ViewerKey = ViewerKey(context) },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetMarketplaceListingBySlug")
            .WithSummary("One listing, by its deep-link handle.");
    }

    /// <summary>
    /// A key for de-duplicating anonymous views.
    /// <para>
    /// <b>The remote address, and only for the length of the de-duplication window.</b> It is never
    /// persisted and never leaves the view tracker; an authenticated caller's identifier is used
    /// instead, which is both more accurate and less to hold. A view counter is not worth keeping a
    /// log of who looked at what.
    /// </para>
    /// </summary>
    private static string? ViewerKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
