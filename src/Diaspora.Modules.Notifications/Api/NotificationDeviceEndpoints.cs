using Diaspora.Modules.Notifications.Application.Devices;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// Where a member can be reached.
/// <para>
/// Separate from Identity's <c>/auth/devices</c> and deliberately so: that surface is about session
/// security — which browser signed in, is it trusted, revoke it — and this one is about delivery.
/// A client registers with both, because they answer different questions with different lifetimes.
/// </para>
/// </summary>
internal static class NotificationDeviceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/devices", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new ListNotificationDevicesQuery(), cancellationToken)))
            .WithName("ListNotificationDevices")
            .WithSummary("The caller's delivery endpoints, including retired ones.");

        // PUT rather than POST: registering the same token twice is the same endpoint, not a second
        // one, and a client retrying on a flaky connection must not create duplicates.
        group.MapPut("/devices", async (
                RegisterNotificationDeviceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RegisterNotificationDeviceCommand { Request = request },
                        cancellationToken)))
            .WithName("RegisterNotificationDevice")
            .WithSummary("Registers or refreshes a push endpoint.");

        // Mapped before the parameterised route so "others" is never read as an identifier. The
        // constraint on the route below already excludes it, and relying on that would make the
        // ordering of two lines in this file the thing standing between a member and a 404.
        group.MapPost("/devices/deactivate-others", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeactivateOtherNotificationDevicesCommand(), cancellationToken)))
            .WithName("DeactivateOtherNotificationDevices")
            .WithSummary("Stops delivering to every endpoint except the one making the request.");

        group.MapDelete("/devices/{id:guid}", async (
                Guid id, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemoveNotificationDeviceCommand { DeviceId = id }, cancellationToken)))
            .WithName("RemoveNotificationDevice")
            .WithSummary("Stops delivering to an endpoint.");
    }
}
