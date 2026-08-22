using Diaspora.Modules.Marketplace.Application.Listings;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// Writing a listing and moving it through its life.
/// <para>
/// Every route names a policy, and every handler behind it additionally asks whether the caller owns
/// <em>this</em> listing. The policy is a necessary condition, never a sufficient one — see the
/// permission matrix.
/// </para>
/// <para>
/// The lifecycle verbs are separate routes rather than a status field on the update, because
/// publishing, archiving and marking sold are decisions with their own permissions and their own
/// consequences, and a PUT that could do any of them by accident is one that eventually does.
/// </para>
/// </summary>
internal static class MarketplaceListingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapAuthoring(group);
        MapLifecycle(group);
        MapMedia(group);
        MapWatchlist(group);
    }

    private static void MapAuthoring(RouteGroupBuilder group)
    {
        group.MapPost("/listings", async (
                [FromBody] CreateListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateListingCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingCreate)
            .WithName("CreateMarketplaceListing")
            .WithSummary("Draft a listing.");

        // BG-07. The commonest real use is a seller listing several near-identical items, who would
        // otherwise retype the description, category, fulfilment and location for each one.
        group.MapPost("/listings/{listingId:guid}/duplicate", async (
                Guid listingId,
                [FromBody] DuplicateListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new DuplicateListingCommand { ListingId = listingId, Title = request?.Title },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingCreate)
            .WithName("DuplicateMarketplaceListing")
            .WithSummary("Copy one of your listings into a new draft.");

        group.MapPut("/listings/{listingId:guid}", async (
                Guid listingId,
                [FromBody] UpdateListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateListingCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("UpdateMarketplaceListing")
            .WithSummary("Update a listing.");

        group.MapPut("/listings/{listingId:guid}/attributes", async (
                Guid listingId,
                [FromBody] SetListingAttributesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetListingAttributesCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("SetMarketplaceListingAttributes")
            .WithSummary("Replace a listing's structured attributes.");

        group.MapPut("/listings/{listingId:guid}/tags", async (
                Guid listingId,
                [FromBody] SetListingTagsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetListingTagsCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("SetMarketplaceListingTags")
            .WithSummary("Replace a listing's tags.");

        group.MapPut("/listings/{listingId:guid}/stock", async (
                Guid listingId,
                [FromBody] SetListingStockRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetListingStockCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("SetMarketplaceListingStock")
            .WithSummary("Restate how many units a listing has.");

        group.MapGet("/listings/mine", async (
                [FromQuery] string? status,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyListingsQuery
                        {
                            Status = status,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("ListMyMarketplaceListings")
            .WithSummary("The caller's own listings, drafts included.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/publish", async (
                Guid listingId,
                [FromBody] PublishListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishListingCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingPublish)
            .WithName("PublishMarketplaceListing")
            .WithSummary("Publish a listing, opening it for offers and reservations.");

        group.MapPost("/listings/{listingId:guid}/archive", async (
                Guid listingId,
                [FromBody] ArchiveListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveListingCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingArchive)
            .WithName("ArchiveMarketplaceListing")
            .WithSummary("Take a listing down from every public surface.");

        group.MapPost("/listings/{listingId:guid}/restore", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreListingCommand { ListingId = listingId }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingRestore)
            .WithName("RestoreMarketplaceListing")
            .WithSummary("Bring an archived or deleted listing back as a draft.");

        group.MapPost("/listings/{listingId:guid}/sold", async (
                Guid listingId,
                [FromBody] MarkListingSoldRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new MarkListingSoldCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingMarkSold)
            .WithName("MarkMarketplaceListingSold")
            .WithSummary("Record a sale made without a reservation.");

        group.MapDelete("/listings/{listingId:guid}", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteListingCommand { ListingId = listingId }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingDeleteOwn)
            .WithName("DeleteMarketplaceListing")
            .WithSummary("Delete a listing. Soft, and reversible by a restore.");

        group.MapPost("/listings/{listingId:guid}/verification", async (
                Guid listingId,
                [FromBody] SubmitVerificationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitListingVerificationCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingSubmitVerification)
            .WithName("SubmitMarketplaceListingVerification")
            .WithSummary("Ask Trust to vouch for a listing.");
    }

    private static void MapMedia(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/media", async (
                Guid listingId,
                [FromBody] AttachListingMediaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AttachListingMediaCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("AttachMarketplaceListingMedia")
            .WithSummary("Attach a photograph, video or document by reference.");

        group.MapDelete("/listings/{listingId:guid}/media/{mediaId:guid}", async (
                Guid listingId,
                Guid mediaId,
                [FromQuery] string? role,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemoveListingMediaCommand
                        {
                            ListingId = listingId,
                            MediaId = mediaId,
                            Role = role
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ListingEditOwn)
            .WithName("RemoveMarketplaceListingMedia")
            .WithSummary("Detach a media reference. The asset itself is never deleted.");
    }

    private static void MapWatchlist(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/save", async (
                Guid listingId,
                [FromBody] SaveListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new Application.Saved.SaveListingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SavedManage)
            .WithName("SaveMarketplaceListing")
            .WithSummary("Add a listing to the caller's watchlist.");

        group.MapDelete("/listings/{listingId:guid}/save", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new Application.Saved.RemoveSavedListingCommand
                        {
                            ListingId = listingId
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SavedManage)
            .WithName("UnsaveMarketplaceListing")
            .WithSummary("Remove a listing from the caller's watchlist.");

        group.MapGet("/saved", async (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new Application.Saved.ListSavedListingsQuery
                        {
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.SavedManage)
            .WithName("ListSavedMarketplaceListings")
            .WithSummary("The caller's watchlist.");
    }
}
