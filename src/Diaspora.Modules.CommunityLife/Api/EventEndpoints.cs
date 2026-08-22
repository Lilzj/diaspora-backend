using Diaspora.Modules.CommunityLife.Application.Events;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The event write surface. Browsing lives in the discovery endpoints.
/// <para>
/// <b>Time, place and capacity each have their own route.</b> Each has a different consequence — a
/// reschedule and a relocation are material changes attendees are told about, and a capacity change
/// is refused below what is committed — and folding them into the patch would give a typo fix the
/// same weight as moving a wedding.
/// </para>
/// </summary>
internal static class EventEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapLifecycle(group);
        MapDetail(group);
        MapMedia(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/events/mine", async (
                [FromQuery] Guid? organizationId,
                [FromQuery(Name = "status")] string[]? statuses,
                [FromQuery] bool? includePast,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyEventsQuery
                        {
                            OrganizationId = organizationId,
                            Statuses = statuses,
                            IncludePast = includePast ?? true,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListMyEvents")
            .WithSummary("The caller's own events, drafts included.");

        group.MapGet("/events/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        Guid.TryParse(idOrSlug, out var id)
                            ? new GetEventQuery { EventId = id }
                            : new GetEventQuery { Slug = idOrSlug },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityEvent")
            .WithSummary("One event, by identifier or by slug.");

        group.MapGet("/events/{eventId:guid}/analytics", async (
                Guid eventId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetEventAnalyticsQuery { EventId = eventId }, cancellationToken)))
            .WithName("GetCommunityEventAnalytics")
            .WithSummary("What an organizer sees about how their event performed.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/events", async (
                CreateEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateEventCommand { Request = request }, cancellationToken)))
            .WithName("CreateCommunityEvent")
            .WithSummary("Drafts an event.");

        // BG-07. The use case that makes duplication matter most here: an event that runs again.
        // The schedule shifts forward in whole weeks so the copy keeps its weekday and time — see
        // DuplicateEventCommandHandler.
        group.MapPost("/events/{eventId:guid}/duplicate", async (
                Guid eventId,
                DuplicateEventRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new DuplicateEventCommand
                        {
                            EventId = eventId,
                            Title = request?.Title,
                            StartsAt = request?.StartsAt,
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityLifePolicies.EventCreate)
            .WithName("DuplicateCommunityEvent")
            .WithSummary("Copy an event you organised into a new draft, on a future date.");

        group.MapPatch("/events/{eventId:guid}", async (
                Guid eventId,
                UpdateEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateEventCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("UpdateCommunityEvent")
            .WithSummary("Edits an event's descriptive content.");

        group.MapPost("/events/{eventId:guid}/publish", async (
                Guid eventId,
                PublishEventRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishEventCommand
                        {
                            EventId = eventId,
                            Request = request ?? new PublishEventRequest()
                        },
                        cancellationToken)))
            .WithName("PublishCommunityEvent")
            .WithSummary("Makes an event discoverable.");

        group.MapPost("/events/{eventId:guid}/cancel", async (
                Guid eventId,
                CancelEventRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelEventCommand
                        {
                            EventId = eventId,
                            Request = request ?? new CancelEventRequest()
                        },
                        cancellationToken)))
            .WithName("CancelCommunityEvent")
            .WithSummary("Calls an event off and releases every place.");

        group.MapPost("/events/{eventId:guid}/archive", async (
                Guid eventId,
                ArchiveEventRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveEventCommand
                        {
                            EventId = eventId,
                            Request = request ?? new ArchiveEventRequest()
                        },
                        cancellationToken)))
            .WithName("ArchiveCommunityEvent")
            .WithSummary("Files an event away.");

        group.MapPost("/events/{eventId:guid}/restore", async (
                Guid eventId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreEventCommand { EventId = eventId }, cancellationToken)))
            .WithName("RestoreCommunityEvent")
            .WithSummary("Brings an archived or deleted event back, as a draft.");

        group.MapDelete("/events/{eventId:guid}", async (
                Guid eventId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteEventCommand { EventId = eventId }, cancellationToken)))
            .WithName("DeleteCommunityEvent")
            .WithSummary("Soft-deletes an event.");
    }

    private static void MapDetail(RouteGroupBuilder group)
    {
        group.MapPost("/events/{eventId:guid}/schedule", async (
                Guid eventId,
                RescheduleEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RescheduleEventCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("RescheduleCommunityEvent")
            .WithSummary("Moves an event in time.");

        group.MapPost("/events/{eventId:guid}/location", async (
                Guid eventId,
                RelocateEventRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RelocateEventCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("RelocateCommunityEvent")
            .WithSummary("Moves an event in space, or between in-person and online.");

        group.MapPut("/events/{eventId:guid}/capacity", async (
                Guid eventId,
                SetEventCapacityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetEventCapacityCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SetCommunityEventCapacity")
            .WithSummary("Changes how many places an event has.");

        group.MapPut("/events/{eventId:guid}/agenda", async (
                Guid eventId,
                SetEventAgendaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetEventAgendaCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SetCommunityEventAgenda")
            .WithSummary("Replaces an event's running order.");

        group.MapPut("/events/{eventId:guid}/speakers", async (
                Guid eventId,
                SetEventSpeakersRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetEventSpeakersCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SetCommunityEventSpeakers")
            .WithSummary("Replaces an event's speaker list.");

        group.MapPut("/events/{eventId:guid}/linked-resources", async (
                Guid eventId,
                SetLinkedResourcesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetLinkedResourcesCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SetCommunityEventLinkedResources")
            .WithSummary("Replaces what an event points at elsewhere on the platform.");

        group.MapPut("/events/{eventId:guid}/featured", async (
                Guid eventId,
                SetFeaturedRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetEventFeaturedCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("SetCommunityEventFeatured")
            .WithSummary("Puts an event on the front of the browse screen. Staff only.");
    }

    private static void MapMedia(RouteGroupBuilder group)
    {
        group.MapPost("/events/{eventId:guid}/media", async (
                Guid eventId,
                MediaAttachmentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AttachEventMediaCommand { EventId = eventId, Request = request },
                        cancellationToken)))
            .WithName("AttachCommunityEventMedia")
            .WithSummary("Attaches an uploaded asset to an event.");

        group.MapDelete("/events/{eventId:guid}/media/{mediaId:guid}", async (
                Guid eventId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DetachEventMediaCommand { EventId = eventId, MediaId = mediaId },
                        cancellationToken)))
            .WithName("DetachCommunityEventMedia")
            .WithSummary("Removes an asset from an event.");
    }
}
