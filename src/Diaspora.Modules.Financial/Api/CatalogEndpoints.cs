using Diaspora.Modules.Financial.Application.Catalog;
using Diaspora.Modules.Financial.Application.Placements;
using Diaspora.Modules.Financial.Application.Promotions;
using Diaspora.Modules.Financial.Application.Purchases;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// What can be bought, and what has been.
/// <para>
/// <b><c>/placements/{subjectType}/{subjectId}</c> is the route every other module actually needs.</b>
/// "Is this listing paid for, and until when" is answerable without the Financial Platform knowing
/// what a listing is, and without the asking module referencing this one.
/// </para>
/// </summary>
internal static class CatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/catalog", async (
                [FromQuery] string? subjectType,
                [FromQuery] Guid? subjectId,
                [FromQuery] string? category,
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMonetizationCatalogQuery
                        {
                            SubjectType = subjectType,
                            SubjectId = subjectId,
                            Category = category,
                            CountryCode = country
                        },
                        cancellationToken)))
            .WithName("GetMonetizationCatalog")
            .WithSummary("What the caller can buy, with a reason on anything they cannot.");

        group.MapPost("/purchases", async (
                PurchaseProductRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PurchaseProductCommand
                        {
                            ProductCode = request.ProductCode,
                            SubjectType = request.SubjectType,
                            SubjectId = request.SubjectId,
                            PromotionCode = request.PromotionCode,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey,
                            StartsAt = request.StartsAt
                        },
                        cancellationToken)))
            .WithName("PurchaseMonetizationProduct")
            .WithSummary("Buys a premium feature, a placement or a credit bundle.");

        group.MapGet("/purchases", async (
                [FromQuery] string? status,
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyPurchasesQuery
                        {
                            Status = status,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyPurchases")
            .WithSummary("Everything the caller has bought. Cursor-paged.");

        group.MapPost("/promotions/redeem", async (
                RedeemPromotionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RedeemPromotionCommand
                        {
                            Code = request.Code,
                            ProductCode = request.ProductCode,
                            CountryCode = request.CountryCode
                        },
                        cancellationToken)))
            .WithName("RedeemPromotion")
            .WithSummary("What a coupon would do, or why it would do nothing.");

        group.MapGet("/placements/{subjectType}/{subjectId:guid}", async (
                string subjectType,
                Guid subjectId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPlacementsForSubjectQuery
                        {
                            SubjectType = subjectType,
                            SubjectId = subjectId
                        },
                        cancellationToken)))
            .WithName("GetPlacementsForSubject")
            .WithSummary("Is this thing paid for, and until when.");

        group.MapGet("/placements/me", async (
                [FromQuery] string? status,
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyPlacementsQuery
                        {
                            Status = status,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyPlacements")
            .WithSummary("Everything the caller has bought a placement for. Cursor-paged.");

        // Declared before the {subjectType}/{subjectId} route would be reached, so a GUID in the
        // first segment resolves a placement rather than being read as a subject type.
        group.MapGet("/placements/{placementId:guid}", async (
                Guid placementId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPlacementQuery { PlacementId = placementId }, cancellationToken)))
            .WithName("GetPlacement")
            .WithSummary(
                "One placement. The route billing.placement.activated deep-links to.");

        group.MapPost("/placements/{placementId:guid}/renew", async (
                Guid placementId,
                RenewPlacementRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RenewPlacementCommand
                        {
                            PlacementId = placementId,
                            ProductCode = request.ProductCode,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("RenewPlacement")
            .WithSummary("Extends the window rather than opening a second placement.");

        group.MapPut("/placements/{placementId:guid}/auto-renew", async (
                Guid placementId,
                SetAutoRenewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetPlacementAutoRenewCommand
                        {
                            PlacementId = placementId,
                            Enabled = request.Enabled
                        },
                        cancellationToken)))
            .WithName("SetPlacementAutoRenew")
            .WithSummary("Records whether it should renew itself when the window closes.");

        group.MapPost("/placements/{placementId:guid}/pause", async (
                Guid placementId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PausePlacementCommand { PlacementId = placementId },
                        cancellationToken)))
            .WithName("PausePlacement")
            .WithSummary("Stops delivery and stops the clock. The paused days are banked.");

        group.MapPost("/placements/{placementId:guid}/resume", async (
                Guid placementId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ResumePlacementCommand { PlacementId = placementId },
                        cancellationToken)))
            .WithName("ResumePlacement")
            .WithSummary("Starts it again and extends the window by exactly the paused duration.");

        group.MapDelete("/placements/{placementId:guid}", async (
                Guid placementId,
                [FromQuery] string? reason,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelPlacementCommand { PlacementId = placementId, Reason = reason },
                        cancellationToken)))
            .WithName("CancelPlacement")
            .WithSummary("Stops a placement. Refunds are a separate decision.");
    }
}
