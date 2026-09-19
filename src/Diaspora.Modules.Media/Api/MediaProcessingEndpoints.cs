using Diaspora.Modules.Media.Application.Processing;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// Processing diagnostics, and the staff levers over them.
/// <para>
/// The per-asset read route is open to any caller who can see the asset, because "why does my
/// photograph have no thumbnail" is a question a member is entitled to an answer to. The two write
/// routes are staff-only: requeueing costs compute, and releasing a quarantine overrides a security
/// control.
/// </para>
/// <para>
/// <b>The quarantine listing exists because the release route had no way to be reached.</b> Both
/// write routes take a media identifier and nothing in the platform produced one for a quarantined
/// file, so in practice nothing was ever released. It is gated harder than the writes it feeds — on
/// <c>media.administer</c> rather than <c>media.processing.manage</c> — because reading a list of
/// other members' refused uploads is a broader disclosure than acting on one you already know of.
/// </para>
/// </summary>
internal static class MediaProcessingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/quarantine", async (
                int? limit, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListQuarantinedMediaQuery { Limit = limit }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Administer)
            .WithName("ListQuarantinedMedia")
            .WithSummary("Every file a scanner refused. The queue the release route needs.");

        group.MapGet("/{mediaId:guid}/processing", async (
                Guid mediaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetProcessingJobQuery { MediaId = mediaId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("GetMediaProcessing")
            .WithSummary("How the work over this asset went, step by step.");

        group.MapPost("/{mediaId:guid}/processing/requeue", async (
                Guid mediaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RequeueProcessingCommand { MediaId = mediaId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageProcessing)
            .WithName("RequeueMediaProcessing")
            .WithSummary("Run processing again — after a codec was deployed, or a fix landed.");

        group.MapPost("/{mediaId:guid}/quarantine/release", async (
                Guid mediaId,
                ReleaseQuarantineBody body,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ReleaseQuarantineCommand { MediaId = mediaId, Reason = body.Reason },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageProcessing)
            .WithName("ReleaseMediaQuarantine")
            .WithSummary("Release a file a scanner flagged. Admins only, always audited.");
    }
}
