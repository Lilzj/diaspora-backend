using Diaspora.Modules.Notifications.Application.Catalog;
using Diaspora.Modules.Notifications.Application.Preferences;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// The settings screen.
/// <para>
/// <b>Two paths, one record.</b> <c>/preferences</c> serves the whole thing;
/// <c>/preferences/communication</c> serves the timing and language half, because the brief names it
/// separately and a client may render it on its own screen. Both write through the same command, so
/// the two can never disagree.
/// </para>
/// </summary>
internal static class NotificationPreferenceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/preferences", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetNotificationPreferencesQuery(), cancellationToken)))
            .WithName("GetNotificationPreferences")
            .WithSummary("Everything the caller can change about how they are contacted.");

        group.MapGet("/preferences/communication", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetCommunicationPreferencesQuery(), cancellationToken)))
            .WithName("GetCommunicationPreferences")
            .WithSummary("Quiet hours, digest schedule, language and time zone.");

        group.MapPut("/preferences", async (
                UpdateNotificationPreferencesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateNotificationPreferencesCommand { Request = request },
                        cancellationToken)))
            .WithName("UpdateNotificationPreferences")
            .WithSummary("Changes whichever sections the request names.");

        // B13.3. What the member asked for, as opposed to what the platform will do — the two are
        // read from the same rows and answer different questions. See the query for why.
        group.MapGet("/preferences/subscriptions", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListNotificationSubscriptionsQuery(), cancellationToken)))
            .WithName("ListNotificationSubscriptions")
            .WithSummary("Per-category email subscription state, with when and how it was set.");

        // B13.3. The one write on this surface that is deliberately anonymous: an unsubscribe link
        // that demanded a session would not work for the member it exists for. AllowAnonymous is
        // explicit rather than inherited, so a future default-authorize policy on the group cannot
        // silently break every footer in every email already sent.
        group.MapPost("/unsubscribe", async (
                RedeemUnsubscribeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RedeemUnsubscribeTokenCommand { Token = request.Token },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("RedeemUnsubscribeToken")
            .WithSummary("Switches off one category's email for the member a signed link names.");

        group.MapGet("/categories", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListNotificationCategoriesQuery(), cancellationToken)))
            .WithName("ListNotificationCategories")
            .WithSummary("The configurable category catalogue.");
    }
}
