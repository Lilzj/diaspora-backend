using Diaspora.Modules.Notifications.Application.Digests;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>The caller's digests.</summary>
internal static class NotificationDigestEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/digests", async (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListDigestsQuery { Cursor = cursor, Limit = limit }, cancellationToken)))
            .WithName("ListDigests")
            .WithSummary("The caller's digests, newest first.");

        group.MapPost("/digests", async (
                GenerateDigestRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new GenerateDigestCommand { Request = request }, cancellationToken)))
            .WithName("GenerateDigest")
            .WithSummary("Assembles a summary now. Refuses an empty window rather than returning one.");
    }
}
