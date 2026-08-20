using Diaspora.Modules.Notifications.Application.Analytics;
using Diaspora.Modules.Notifications.Application.Delivery;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// The operations surface. Staff only.
/// <para>
/// Everything here answers "did it go" and never "what did it say". The delivery history redacts the
/// words before the response leaves, so a support session is not a disclosure.
/// </para>
/// </summary>
internal static class NotificationOperationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{id:guid}/deliveries", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetDeliveryHistoryQuery { NotificationId = id }, cancellationToken)))
            .WithName("GetNotificationDeliveryHistory")
            .WithSummary("Every channel's record for one message. The words are redacted.");

        group.MapPost("/{id:guid}/deliveries/{channel}/retry", async (
                Guid id,
                string channel,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RetryNotificationDeliveryCommand { NotificationId = id, Channel = channel },
                        cancellationToken)))
            .WithName("RetryNotificationDelivery")
            .WithSummary("Asks for one more attempt on a failed channel.");

        group.MapGet("/analytics", async (
                [FromQuery] DateOnly? from,
                [FromQuery] DateOnly? to,
                [FromQuery] string? category,
                [FromQuery] string? channel,
                [FromQuery] string? template,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNotificationAnalyticsQuery
                        {
                            From = from,
                            To = to,
                            CategoryKey = category,
                            Channel = channel,
                            TemplateKey = template
                        },
                        cancellationToken)))
            .WithName("GetNotificationAnalytics")
            .WithSummary("Delivery, open, click and failure rates over a window.");
    }
}
