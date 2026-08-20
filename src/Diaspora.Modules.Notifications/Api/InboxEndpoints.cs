using Diaspora.Modules.Notifications.Application.Inbox;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// The member's own inbox.
/// <para>
/// <b>The collection is at the module root</b> — <c>/api/v1/notifications</c> — because that is what
/// the client already calls the screen, and because a member has exactly one inbox so there is
/// nothing to name it by. Every route here is implicitly scoped to the caller; none takes a user
/// identifier, which is what makes reading somebody else's inbox unexpressible rather than merely
/// forbidden.
/// </para>
/// </summary>
internal static class InboxEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/", async (
                [FromQuery] string? scope,
                [FromQuery] bool? unreadOnly,
                [FromQuery(Name = "category")] string[]? categories,
                [FromQuery(Name = "priority")] string[]? priorities,
                [FromQuery] string? search,
                [FromQuery] string? source,
                [FromQuery] string? entityType,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetInboxQuery
                        {
                            Scope = scope,
                            UnreadOnly = unreadOnly,
                            Categories = categories,
                            Priorities = priorities,
                            Search = search,
                            SourceModule = source,
                            EntityType = entityType,
                            From = from,
                            To = to,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("GetInbox")
            .WithSummary("The caller's notifications, filtered and searched.");

        group.MapGet("/unread-count", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetUnreadCountQuery(), cancellationToken)))
            .WithName("GetUnreadCount")
            .WithSummary("The badge, from the maintained counter.");

        group.MapGet("/{id:guid}", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNotificationQuery { NotificationId = id }, cancellationToken)))
            .WithName("GetNotification")
            .WithSummary("One of the caller's notifications, with its delivery record.");

        group.MapPost("/{id:guid}/read", async (
                Guid id,
                [FromQuery] string? channel,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new MarkNotificationReadCommand { NotificationId = id, Channel = channel },
                        cancellationToken)))
            .WithName("MarkNotificationRead")
            .WithSummary("Marks a notification read.");

        group.MapPost("/{id:guid}/unread", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new MarkNotificationUnreadCommand { NotificationId = id }, cancellationToken)))
            .WithName("MarkNotificationUnread")
            .WithSummary("Puts a notification back to unread.");

        group.MapPost("/read-all", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new MarkAllNotificationsReadCommand(), cancellationToken)))
            .WithName("MarkAllNotificationsRead")
            .WithSummary("Marks everything read, in batches, and reports what is left.");

        group.MapPost("/{id:guid}/click", async (
                Guid id,
                RecordNotificationClickRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordNotificationClickCommand
                        {
                            NotificationId = id,
                            ActionKey = request.ActionKey,
                            Channel = request.Channel
                        },
                        cancellationToken)))
            .WithName("RecordNotificationClick")
            .WithSummary("Records that the caller followed a notification somewhere.");

        group.MapPost("/{id:guid}/dismiss", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DismissNotificationCommand { NotificationId = id }, cancellationToken)))
            .WithName("DismissNotification")
            .WithSummary("Records that the caller refused a notification.");

        group.MapPost("/{id:guid}/archive", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveNotificationCommand { NotificationId = id }, cancellationToken)))
            .WithName("ArchiveNotification")
            .WithSummary("Files a notification.");

        // Named "restore" to match the brief and the client's vocabulary. It is the un-archive; the
        // un-delete is deliberately not offered, because a member who deleted something meant it.
        group.MapPost("/{id:guid}/restore", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreNotificationCommand { NotificationId = id }, cancellationToken)))
            .WithName("RestoreNotification")
            .WithSummary("Brings a filed notification back to the inbox.");

        group.MapPut("/{id:guid}/pin", async (
                Guid id,
                [FromQuery] bool? pinned,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PinNotificationCommand { NotificationId = id, IsPinned = pinned ?? true },
                        cancellationToken)))
            .WithName("PinNotification")
            .WithSummary("Pins or unpins a notification.");

        group.MapPut("/{id:guid}/star", async (
                Guid id,
                [FromQuery] bool? starred,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new StarNotificationCommand { NotificationId = id, IsStarred = starred ?? true },
                        cancellationToken)))
            .WithName("StarNotification")
            .WithSummary("Stars or unstars a notification.");

        group.MapDelete("/{id:guid}", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteNotificationCommand { NotificationId = id }, cancellationToken)))
            .WithName("DeleteNotification")
            .WithSummary("Deletes a notification. Soft, and never published.");
    }
}
