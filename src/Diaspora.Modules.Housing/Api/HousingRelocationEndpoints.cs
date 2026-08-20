using Diaspora.Modules.Housing.Application.Reservations;
using Diaspora.Modules.Housing.Application.Viewings;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// The relocation workflow: viewings and reservations.
/// <para>
/// <b>Every route here is private to two people, and the handlers enforce that rather than the
/// routes.</b> A viewing and a reservation belong to the member and the landlord; staff get no
/// bypass, because reading a named person's appointments — where they are going and when a property
/// will be empty — is a different act from moderating a public listing.
/// </para>
/// <para>
/// <b>The viewing routes are the ones that unlock addresses</b>, which is why scheduling sits
/// behind a verified-tier permission rather than a merely authenticated one.
/// </para>
/// </summary>
internal static class HousingRelocationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapViewings(group);
        MapReservations(group);
    }

    private static void MapViewings(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/viewings", async (
                Guid listingId,
                [FromBody] RequestViewingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RequestViewingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ViewingSchedule)
            .WithName("RequestHousingViewing")
            .WithSummary("Ask to see a property.");

        group.MapPost("/viewings/{viewingId:guid}", async (
                Guid viewingId,
                [FromBody] UpdateViewingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateViewingCommand
                        {
                            ViewingId = viewingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ViewingManage)
            .WithName("UpdateHousingViewing")
            .WithSummary("Schedule, complete, cancel or record a no-show.");

        group.MapGet("/viewings", async (
                [FromQuery] string? side,
                [FromQuery] bool? openOnly,
                [FromQuery] Guid? listingId,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListViewingsQuery
                        {
                            Side = side,
                            OpenOnly = openOnly ?? true,
                            ListingId = listingId,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ViewingManage)
            .WithName("ListHousingViewings")
            .WithSummary("The caller's viewings, as a member or as a landlord.");

        group.MapGet("/viewings/{viewingId:guid}", async (
                Guid viewingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetViewingQuery { ViewingId = viewingId }, cancellationToken)))
            .RequireAuthorization(HousingPolicies.ViewingManage)
            .WithName("GetHousingViewing")
            .WithSummary("One viewing, with every step of it.");
    }

    private static void MapReservations(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/reservations", async (
                Guid listingId,
                [FromBody] CreateHousingReservationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateHousingReservationCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ReservationCreate)
            .WithName("CreateHousingReservation")
            .WithSummary("Hold a property against the caller.");

        group.MapPost("/reservations/{reservationId:guid}", async (
                Guid reservationId,
                [FromBody] UpdateReservationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateHousingReservationCommand
                        {
                            ReservationId = reservationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ReservationManage)
            .WithName("UpdateHousingReservation")
            .WithSummary("Accept, decline, sign, occupy, cancel or extend.");

        group.MapGet("/reservations", async (
                [FromQuery] string? side,
                [FromQuery] bool? activeOnly,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListHousingReservationsQuery
                        {
                            Side = side,
                            ActiveOnly = activeOnly ?? true,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ReservationManage)
            .WithName("ListHousingReservations")
            .WithSummary("The caller's reservations, as a member or as a landlord.");

        group.MapGet("/reservations/{reservationId:guid}", async (
                Guid reservationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetHousingReservationQuery { ReservationId = reservationId },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ReservationManage)
            .WithName("GetHousingReservation")
            .WithSummary("One reservation, with its history.");
    }
}
