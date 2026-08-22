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
/// A landlord's own properties: authoring, lifecycle and media.
/// <para>
/// <b>Every route here is scoped to the caller inside its handler.</b> Neither the identifier in
/// the path nor the policy on the route is sufficient — the handler asks whether the caller owns
/// the property, and answers not-found when they do not, because a forbidden would confirm the
/// listing exists.
/// </para>
/// </summary>
internal static class HousingListingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapAuthoring(group);
        MapLifecycle(group);
        MapMedia(group);
    }

    private static void MapAuthoring(RouteGroupBuilder group)
    {
        group.MapPost("/listings", async (
                [FromBody] CreateHousingListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateHousingListingCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingCreate)
            .WithName("CreateHousingListing")
            .WithSummary("Draft a property. Always private until published.");

        // BG-07. A landlord listing several units in one building would otherwise retype the whole
        // form each time. The address and the photographs are deliberately NOT carried over — see
        // DuplicateHousingListingCommandHandler.
        group.MapPost("/listings/{listingId:guid}/duplicate", async (
                Guid listingId,
                [FromBody] DuplicateHousingListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new DuplicateHousingListingCommand { ListingId = listingId, Title = request?.Title },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingCreate)
            .WithName("DuplicateHousingListing")
            .WithSummary("Copy one of your properties into a new draft.");

        group.MapPatch("/listings/{listingId:guid}", async (
                Guid listingId,
                [FromBody] UpdateHousingListingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateHousingListingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("UpdateHousingListing")
            .WithSummary("Revise a property. Omitted fields are left alone.");

        group.MapPut("/listings/{listingId:guid}/amenities", async (
                Guid listingId,
                [FromBody] SetHousingAmenitiesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetHousingAmenitiesCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("SetHousingAmenities")
            .WithSummary("Replace the amenity list.");

        group.MapPut("/listings/{listingId:guid}/house-rules", async (
                Guid listingId,
                [FromBody] SetHouseRulesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetHouseRulesCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("SetHouseRules")
            .WithSummary("Replace the house rules.");

        group.MapPut("/listings/{listingId:guid}/roommate-preferences", async (
                Guid listingId,
                [FromBody] SetRoommatePreferencesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetRoommatePreferencesCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("SetRoommatePreferences")
            .WithSummary("Say what a shared household is looking for.");

        group.MapPut("/listings/{listingId:guid}/availability", async (
                Guid listingId,
                [FromBody] SetAvailabilityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetHousingAvailabilityCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("SetHousingAvailability")
            .WithSummary("Restate when a property is available and how many units it has.");

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
                        new ListMyHousingListingsQuery
                        {
                            Status = status,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("ListMyHousingListings")
            .WithSummary("The caller's own properties, drafts included.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/publish", async (
                Guid listingId,
                [FromBody] PublishHousingListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishHousingListingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingPublish)
            .WithName("PublishHousingListing")
            .WithSummary("Put a property on the market.");

        group.MapPost("/listings/{listingId:guid}/archive", async (
                Guid listingId,
                [FromBody] ArchiveHousingListingRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveHousingListingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingArchive)
            .WithName("ArchiveHousingListing")
            .WithSummary("Take a property off the market, temporarily or for good.");

        group.MapPost("/listings/{listingId:guid}/restore", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreHousingListingCommand { ListingId = listingId },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingRestore)
            .WithName("RestoreHousingListing")
            .WithSummary("Bring an archived or deleted property back, as a draft.");

        group.MapDelete("/listings/{listingId:guid}", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteHousingListingCommand { ListingId = listingId },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingDeleteOwn)
            .WithName("DeleteHousingListing")
            .WithSummary("Soft-delete a property.");

        group.MapPost("/listings/{listingId:guid}/occupied", async (
                Guid listingId,
                [FromBody] MarkOccupiedRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new MarkHousingOccupiedCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingMarkOccupied)
            .WithName("MarkHousingOccupied")
            .WithSummary("Record that somebody has moved in.");

        group.MapPost("/listings/{listingId:guid}/verification", async (
                Guid listingId,
                [FromBody] SubmitHousingVerificationRequest request,
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
            .RequireAuthorization(HousingPolicies.ListingSubmitVerification)
            .WithName("SubmitHousingListingVerification")
            .WithSummary("Ask Trust to vouch for a property. Returns a case identifier.");
    }

    private static void MapMedia(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/media", async (
                Guid listingId,
                [FromBody] AttachHousingMediaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AttachHousingMediaCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("AttachHousingMedia")
            .WithSummary("Attach a photograph, floor plan or document, by reference.");

        group.MapDelete("/listings/{listingId:guid}/media/{mediaId:guid}", async (
                Guid listingId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DetachHousingMediaCommand
                        {
                            ListingId = listingId,
                            MediaId = mediaId
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ListingEditOwn)
            .WithName("DetachHousingMedia")
            .WithSummary("Remove a media reference. Never deletes the asset.");
    }
}
