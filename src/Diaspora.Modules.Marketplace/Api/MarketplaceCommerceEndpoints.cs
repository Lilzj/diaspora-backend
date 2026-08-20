using Diaspora.Modules.Marketplace.Application.Conversations;
using Diaspora.Modules.Marketplace.Application.Offers;
using Diaspora.Modules.Marketplace.Application.Reservations;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// Buying: offers, reservations and the conversation references between the two parties.
/// <para>
/// <b>Every route here is scoped to the caller inside its handler.</b> A negotiation and a
/// reservation are private to their two parties, and neither the identifier in the path nor the
/// policy on the route is sufficient — the handler asks whether the caller is one of them, and
/// answers not-found when they are not.
/// </para>
/// </summary>
internal static class MarketplaceCommerceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapOffers(group);
        MapReservations(group);
        MapConversations(group);
    }

    private static void MapOffers(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/offers", async (
                Guid listingId,
                [FromBody] MakeOfferRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new MakeOfferCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferMake)
            .WithName("MakeMarketplaceOffer")
            .WithSummary("Open a negotiation on a listing.");

        group.MapPost("/offers/{offerId:guid}/counter", async (
                Guid offerId,
                [FromBody] CounterOfferRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CounterOfferCommand { OfferId = offerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("CounterMarketplaceOffer")
            .WithSummary("Answer a negotiation with a different number.");

        group.MapPost("/offers/{offerId:guid}/settle", async (
                Guid offerId,
                [FromBody] SettleOfferRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SettleOfferCommand { OfferId = offerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("SettleMarketplaceOffer")
            .WithSummary("Accept, decline or withdraw an offer.");

        group.MapGet("/offers", async (
                [FromQuery] string? side,
                [FromQuery] bool? openOnly,
                [FromQuery] bool? awaitingMe,
                [FromQuery] Guid? listingId,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListOffersQuery
                        {
                            Side = side,
                            OpenOnly = openOnly ?? true,
                            AwaitingMeOnly = awaitingMe ?? false,
                            ListingId = listingId,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("ListMarketplaceOffers")
            .WithSummary("The caller's negotiations, buying or selling.");

        group.MapGet("/offers/{offerId:guid}", async (
                Guid offerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOfferQuery { OfferId = offerId }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("GetMarketplaceOffer")
            .WithSummary("One negotiation, with every round of it.");
    }

    private static void MapReservations(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/reservations", async (
                Guid listingId,
                [FromBody] CreateReservationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateReservationCommand { ListingId = listingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ReservationCreate)
            .WithName("CreateMarketplaceReservation")
            .WithSummary("Hold stock against the caller.");

        group.MapPost("/reservations/{reservationId:guid}", async (
                Guid reservationId,
                [FromBody] UpdateReservationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateReservationCommand
                        {
                            ReservationId = reservationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ReservationManage)
            .WithName("UpdateMarketplaceReservation")
            .WithSummary("Acknowledge, complete or cancel a reservation.");

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
                        new ListReservationsQuery
                        {
                            Side = side,
                            ActiveOnly = activeOnly ?? true,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ReservationManage)
            .WithName("ListMarketplaceReservations")
            .WithSummary("The caller's reservations, buying or selling.");

        group.MapGet("/reservations/{reservationId:guid}", async (
                Guid reservationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetReservationQuery { ReservationId = reservationId },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ReservationManage)
            .WithName("GetMarketplaceReservation")
            .WithSummary("One reservation, with its history.");
    }

    private static void MapConversations(RouteGroupBuilder group)
    {
        group.MapPost("/listings/{listingId:guid}/conversation", async (
                Guid listingId,
                [FromBody] StartConversationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new StartConversationCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("StartMarketplaceConversation")
            .WithSummary("Ask to talk to a seller. Returns a reference, never a thread.");

        group.MapGet("/conversations", async (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListConversationsQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.OfferRespond)
            .WithName("ListMarketplaceConversations")
            .WithSummary("The caller's conversation references.");
    }
}
