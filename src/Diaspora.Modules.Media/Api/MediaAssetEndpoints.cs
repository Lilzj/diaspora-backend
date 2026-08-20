using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using Diaspora.SharedKernel.Pagination;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>Reading, describing, attaching and deleting assets.</summary>
internal static class MediaAssetEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{mediaId:guid}", async (
                Guid mediaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetMediaQuery { MediaId = mediaId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("GetMedia")
            .WithSummary("One asset: its metadata, its variants and its URLs.");

        group.MapGet("/", async (
                string? kind,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListMyMediaQuery
                    {
                        Kind = kind,
                        Pagination = new PaginationRequest
                        {
                            Page = page ?? PaginationRequest.DefaultPage,
                            PageSize = pageSize ?? PaginationRequest.DefaultPageSize
                        }
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("ListMyMedia")
            .WithSummary("The caller's own media library, newest first.");

        group.MapPatch("/{mediaId:guid}", async (
                Guid mediaId,
                UpdateMediaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UpdateMediaCommand { MediaId = mediaId, Request = request },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Manage)
            .WithName("UpdateMedia")
            .WithSummary("Change an asset's alt text, caption or visibility.");

        group.MapDelete("/{mediaId:guid}", async (
                Guid mediaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeleteMediaCommand { MediaId = mediaId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Delete)
            .WithName("DeleteMedia")
            .WithSummary("Delete an asset. Soft: the record survives for audit.");

        group.MapPost("/attachments", async (
                AttachMediaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new AttachMediaCommand { Request = request }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Manage)
            .WithName("AttachMedia")
            .WithSummary("Claim an asset for an owning entity. Takes it out of the orphan sweep.");

        group.MapPost("/{mediaId:guid}/signed-url", async (
                Guid mediaId,
                CreateSignedUrlRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateSignedUrlCommand
                    {
                        MediaId = mediaId,
                        Request = request ?? new CreateSignedUrlRequest()
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.SignedAccess)
            .WithName("CreateMediaSignedUrl")
            .WithSummary("Mint a short-lived link to a private asset.");

        group.MapPost("/{mediaId:guid}/variants", async (
                Guid mediaId,
                RequestVariantRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RequestVariantCommand { MediaId = mediaId, Request = request },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Transform)
            .WithName("RequestMediaVariant")
            .WithSummary("Produce a named rendition that does not exist yet.");

        // Deduplication and reuse detection. Returns only what the caller may already see —
        // otherwise this answers "does a file with these exact bytes exist on the platform"
        // for anyone holding a copy, which for a leaked document is the confirmation an
        // attacker wants.
        group.MapGet("/by-hash/{contentHash}", async (
                string contentHash,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new FindMediaByHashQuery { ContentHash = contentHash }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("FindMediaByHash")
            .WithSummary("Find assets with the same bytes. Feeds Trust's reuse detection.");
    }
}
