using Diaspora.Modules.Media.Application.Uploads;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// The upload lifecycle: initialise, send, pause, resume, complete, cancel, resume-after-crash.
/// <para>
/// <b>Why the upload permission is checked per media kind rather than once.</b> Uploading a
/// photograph opens at the onboarding tier because adding a profile picture is part of
/// finishing registration; uploading a video opens at verified because half a gigabyte of
/// storage plus a transcode is a real cost. A single <c>media.upload</c> policy would have to
/// pick one of those, and either choice is wrong for the other case. So the routes carry the
/// broadest policy and the handler enforces the kind-specific one — which is also what lets
/// the error name the tier a member needs to reach.
/// </para>
/// </summary>
internal static class UploadEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/uploads", async (
                StartUploadRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new StartUploadCommand { Request = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            .WithName("StartMediaUpload")
            .WithSummary("Open an upload. The server picks the protocol and the chunk size.");

        // Multipart rather than JSON, because this route carries bytes. The size cap is the
        // session's chunk size plus headroom, enforced by the host's request-body limit — a
        // route that accepted an unbounded body would be a memory exhaustion vector regardless
        // of what the session declared.
        group.MapPut("/uploads/{sessionId:guid}/chunks/{chunkNumber:int}", async (
                Guid sessionId,
                int chunkNumber,
                HttpRequest httpRequest,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UploadChunkCommand
                    {
                        SessionId = sessionId,
                        ChunkNumber = chunkNumber,
                        Content = httpRequest.Body,
                        ChunkHash = httpRequest.Headers["X-Content-SHA256"].FirstOrDefault()
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            // The one route on this platform where a single legitimate action is dozens of
            // requests: a hundred-megabyte file is twenty chunks. Metered against the
            // authenticated budget every other screen draws on, one upload would stop the app
            // loading; a bucket of its own bounds upload abuse without that side effect. Granted
            // to the chunk PUT alone — /uploads/direct is one request per file and belongs on the
            // ordinary budget, where it is already the tighter of the two.
            .RequireRateLimiting(RateLimitPolicyNames.Upload)
            .WithName("UploadMediaChunk")
            .WithSummary("Send one part. Idempotent: re-sending an identical part is a no-op.");

        group.MapPost("/uploads/{sessionId:guid}/complete", async (
                Guid sessionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CompleteUploadCommand { SessionId = sessionId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            .WithName("CompleteMediaUpload")
            .WithSummary("Assemble the parts and produce an asset.");

        group.MapPut("/uploads/{sessionId:guid}/state", async (
                Guid sessionId,
                SetUploadStateRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SetUploadStateCommand { SessionId = sessionId, State = request.State },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            .WithName("SetMediaUploadState")
            .WithSummary("Pause, resume or cancel an upload.");

        group.MapDelete("/uploads/{sessionId:guid}", async (
                Guid sessionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SetUploadStateCommand { SessionId = sessionId, State = "cancelled" },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            .WithName("CancelMediaUpload")
            .WithSummary("Abandon an upload and discard its parts.");

        group.MapGet("/uploads/{sessionId:guid}", async (
                Guid sessionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetUploadSessionQuery { SessionId = sessionId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("GetMediaUpload")
            .WithSummary("Read an upload's state and which parts have arrived. The resume call.");

        group.MapGet("/uploads", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListMyUploadsQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("ListMyMediaUploads")
            .WithSummary("The caller's open uploads. What was I uploading, after a reinstall.");

        // The path almost every upload in this app takes: one small picker result, one
        // request. Shares every validation and processing step with the chunked path.
        group.MapPost("/uploads/direct", async (
                HttpRequest httpRequest,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                if (!httpRequest.HasFormContentType)
                {
                    return PlatformResults.Ok(
                        context,
                        SharedKernel.Results.Result.Failure(
                            Domain.Errors.MediaErrors.UploadEmpty));
                }

                var form = await httpRequest.ReadFormAsync(cancellationToken);
                var file = form.Files.Count > 0 ? form.Files[0] : null;

                if (file is null)
                {
                    return PlatformResults.Ok(
                        context,
                        SharedKernel.Results.Result.Failure(
                            Domain.Errors.MediaErrors.UploadEmpty));
                }

                await using var content = file.OpenReadStream();

                var result = await sender.Send(
                    new DirectUploadCommand
                    {
                        Kind = form["kind"].FirstOrDefault() ?? "image",
                        ContentType = file.ContentType,
                        FileName = file.FileName,
                        Content = content,
                        OwnerType = form["ownerType"].FirstOrDefault(),
                        OwnerId = Guid.TryParse(form["ownerId"].FirstOrDefault(), out var owner)
                            ? owner
                            : null,
                        Role = form["role"].FirstOrDefault()
                    },
                    cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(MediaPolicies.UploadImage)
            .DisableAntiforgery()
            .WithName("UploadMediaDirect")
            .WithSummary("Upload a small file in one request.");
    }
}
