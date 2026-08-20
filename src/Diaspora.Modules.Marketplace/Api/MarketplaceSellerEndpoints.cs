using Diaspora.Modules.Marketplace.Application.Analytics;
using Diaspora.Modules.Marketplace.Application.Dashboards;
using Diaspora.Modules.Marketplace.Application.Sellers;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>Selling: the profile, the storefront and the seller's own figures.</summary>
internal static class MarketplaceSellerEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/sellers", async (
                [FromBody] RegisterSellerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RegisterSellerCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SellerCreate)
            .WithName("RegisterMarketplaceSeller")
            .WithSummary("Set up a seller profile.");

        group.MapPut("/sellers/me", async (
                [FromBody] UpdateSellerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateSellerCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SellerManage)
            .WithName("UpdateMarketplaceSeller")
            .WithSummary("Update the caller's own seller profile.");

        group.MapPost("/sellers/me/verification", async (
                [FromBody] SubmitVerificationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitSellerVerificationCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SellerSubmitVerification)
            .WithName("SubmitMarketplaceSellerVerification")
            .WithSummary("Ask Trust to verify the caller's seller profile.");

        group.MapGet("/sellers/me/dashboard", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetSellerDashboardQuery(), cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SellerManage)
            .WithName("GetMarketplaceSellerDashboard")
            .WithSummary("Everything a seller needs on opening the app.");

        group.MapGet("/sellers/{sellerId:guid}", async (
                Guid sellerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetStorefrontQuery { SellerId = sellerId }, cancellationToken)))
            .AllowAnonymous()
            .WithName("GetMarketplaceStorefront")
            .WithSummary("A seller's storefront.");

        group.MapGet("/sellers/by-slug/{slug}", async (
                string slug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetStorefrontQuery { Slug = slug }, cancellationToken)))
            .AllowAnonymous()
            .WithName("GetMarketplaceStorefrontBySlug")
            .WithSummary("A seller's storefront, by handle.");

        group.MapGet("/listings/{listingId:guid}/analytics", async (
                Guid listingId,
                [FromQuery] int? days,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetListingAnalyticsQuery { ListingId = listingId, Days = days },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ViewAnalytics)
            .WithName("GetMarketplaceListingAnalytics")
            .WithSummary("How one listing has performed.");
    }
}
