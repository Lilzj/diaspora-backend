using Diaspora.Modules.Marketplace.Application.Analytics;
using Diaspora.Modules.Marketplace.Application.Moderation;
using Diaspora.Modules.Marketplace.Application.Promotions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// The safety surface and the staff tools.
/// <para>
/// <b>Reporting is the one route here a member can reach</b>, and it sits at the low permission
/// floor and survives a restriction — because a restriction that silenced the person best placed to
/// flag a scam would be the platform working against itself. Everything else is staff, and none of
/// it acts on a member's listing: that is a Trust moderation case's decision.
/// </para>
/// </summary>
internal static class MarketplaceModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/listings/{listingId:guid}/report", async (
                Guid listingId,
                [FromBody] ReportListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReportListingCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.Report)
            .WithName("ReportMarketplaceListing")
            .WithSummary("Report a listing to the moderation queue.");

        group.MapGet("/moderation/queue", async (
                [FromQuery] string? status,
                [FromQuery] string? reason,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListModerationQueueQuery
                        {
                            Status = status,
                            Reason = reason,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.Moderate)
            .WithName("ListMarketplaceModerationQueue")
            .WithSummary("The marketplace moderation queue.");

        // Hiding, suspending and restoring a listing, closing a report and suspending a seller were
        // actions here until 7 Nov 2026. A listing is now decided only on the Trust moderation case
        // (/trust/moderation/queue/{id}/decision), which carries the decision out through
        // MarketplaceListingModerationTarget and closes these reports. Seller suspension, a staff
        // action on a member with no case at all, waits for the strikes that replace it.

        group.MapGet("/moderation/trends", async (
                [FromQuery] string? groupBy,
                [FromQuery] string? country,
                [FromQuery] string? category,
                [FromQuery] int? days,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMarketplaceTrendsQuery
                        {
                            GroupBy = groupBy,
                            CountryCode = country,
                            CategoryKey = category,
                            Days = days,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.Moderate)
            .WithName("GetMarketplaceTrends")
            .WithSummary("What is moving, by category or by country.");

        group.MapPost("/listings/{listingId:guid}/promotions", async (
                Guid listingId,
                [FromBody] CreatePromotionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreatePromotionCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ManagePromotions)
            .WithName("CreateMarketplacePromotion")
            .WithSummary("Grant a listing extra placement for a window.");

        group.MapDelete("/promotions/{promotionId:guid}", async (
                Guid promotionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelPromotionCommand { PromotionId = promotionId },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ManagePromotions)
            .WithName("CancelMarketplacePromotion")
            .WithSummary("End a promotion before its window closes.");
    }
}
