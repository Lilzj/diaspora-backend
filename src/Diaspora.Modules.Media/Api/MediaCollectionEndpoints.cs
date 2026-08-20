using Diaspora.Modules.Media.Application.Collections;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>Galleries, attachment sets and their ordering.</summary>
internal static class MediaCollectionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/collections", async (
                CreateCollectionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CreateCollectionCommand { Request = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageCollections)
            .WithName("CreateMediaCollection")
            .WithSummary("Create a gallery. Idempotent by owner and kind.");

        group.MapGet("/collections/{collectionId:guid}", async (
                Guid collectionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetCollectionQuery { CollectionId = collectionId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("GetMediaCollection")
            .WithSummary("A collection and every asset in it, in render order.");

        group.MapPost("/collections/{collectionId:guid}/items", async (
                Guid collectionId,
                AddCollectionItemRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ModifyCollectionCommand
                    {
                        CollectionId = collectionId,
                        Operation = "add",
                        MediaId = request.MediaId,
                        Caption = request.Caption
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageCollections)
            .WithName("AddMediaCollectionItem")
            .WithSummary("Place an asset at the end of a collection.");

        group.MapDelete("/collections/{collectionId:guid}/items/{mediaId:guid}", async (
                Guid collectionId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ModifyCollectionCommand
                    {
                        CollectionId = collectionId,
                        Operation = "remove",
                        MediaId = mediaId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageCollections)
            .WithName("RemoveMediaCollectionItem")
            .WithSummary("Remove an asset and close the gap it left.");

        // PUT rather than PATCH: the body is the complete order, and a partial reorder has no
        // defined meaning — three of five identifiers does not say where the other two go.
        group.MapPut("/collections/{collectionId:guid}/order", async (
                Guid collectionId,
                ReorderCollectionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ModifyCollectionCommand
                    {
                        CollectionId = collectionId,
                        Operation = "reorder",
                        MediaIds = request.MediaIds
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageCollections)
            .WithName("ReorderMediaCollection")
            .WithSummary("Set the render order. The body must be the whole membership.");

        group.MapPut("/collections/{collectionId:guid}/primary/{mediaId:guid}", async (
                Guid collectionId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ModifyCollectionCommand
                    {
                        CollectionId = collectionId,
                        Operation = "promote",
                        MediaId = mediaId
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.ManageCollections)
            .WithName("PromoteMediaCollectionItem")
            .WithSummary("Make this the cover photo.");
    }
}
