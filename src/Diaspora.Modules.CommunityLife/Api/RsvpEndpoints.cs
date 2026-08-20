using Diaspora.Modules.CommunityLife.Application.Rsvps;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The attendance surface.
/// <para>
/// <b>The caller's own response is a singleton resource, not a collection.</b> One member has one
/// standing at one event, and a route that let them create a second would be a route that let one
/// person hold two places.
/// </para>
/// </summary>
internal static class RsvpEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPut("/events/{eventId:guid}/rsvp", async (
                Guid eventId,
                SubmitRsvpRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitRsvpCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SubmitRsvp")
            .WithSummary("Says whether the caller is coming, and with how many people.");

        group.MapDelete("/events/{eventId:guid}/rsvp", async (
                Guid eventId,
                [FromQuery] string? reason,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawRsvpCommand
                        {
                            EventId = eventId,
                            Request = new WithdrawRsvpRequest { Reason = reason }
                        },
                        cancellationToken)))
            .WithName("WithdrawRsvp")
            .WithSummary("Withdraws the caller from an event.");

        group.MapGet("/rsvps/mine", async (
                [FromQuery] bool? upcomingOnly,
                [FromQuery(Name = "status")] string[]? statuses,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyRsvpsQuery
                        {
                            UpcomingOnly = upcomingOnly ?? true,
                            Statuses = statuses,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListMyRsvps")
            .WithSummary("What the caller has said they are going to.");

        group.MapGet("/events/{eventId:guid}/attendees", async (
                Guid eventId,
                [FromQuery(Name = "status")] string[]? statuses,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListAttendeesQuery
                        {
                            EventId = eventId,
                            Statuses = statuses,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListEventAttendees")
            .WithSummary("Who is coming to an event.");

        group.MapPost("/events/{eventId:guid}/check-ins", async (
                Guid eventId,
                CheckInRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RecordCheckInCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("RecordEventCheckIn")
            .WithSummary("Records somebody arriving at an event.");
    }
}
