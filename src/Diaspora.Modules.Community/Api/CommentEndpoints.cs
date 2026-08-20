using Diaspora.Modules.Community.Application.Comments;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>
/// Comments, on anything.
/// <para>
/// Two shapes of route, deliberately. <c>/posts/{id}/comments</c> is the convenient one for the
/// commonest case; <c>/content/{type}/{id}/comments</c> is the general one that serves guides,
/// events and every module that registers a content source. Both dispatch the same command.
/// </para>
/// </summary>
internal static class CommentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/posts/{postId:guid}/comments", async (
                Guid postId,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommentThreadQuery
                        {
                            ContentType = "post",
                            ContentId = postId,
                            Cursor = cursor,
                            Limit = limit ?? CommunityLimits.DefaultFeedPageSize
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("GetCommunityPostComments")
            .WithSummary("A page of the comment thread on a post.");

        group.MapGet("/content/{contentType}/{contentId:guid}/comments", async (
                string contentType,
                Guid contentId,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommentThreadQuery
                        {
                            ContentType = contentType,
                            ContentId = contentId,
                            Cursor = cursor,
                            Limit = limit ?? CommunityLimits.DefaultFeedPageSize
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("GetCommunityContentComments")
            .WithSummary("A comment thread on any content type a module has claimed.");

        group.MapPost("/posts/{postId:guid}/comments", async (
                Guid postId,
                CreateCommentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new AddCommentCommand
                        {
                            ContentType = "post", ContentId = postId, Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Comment)
            .WithName("AddCommunityPostComment")
            .WithSummary("Comment on a post.");

        group.MapPost("/content/{contentType}/{contentId:guid}/comments", async (
                string contentType,
                Guid contentId,
                CreateCommentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new AddCommentCommand
                        {
                            ContentType = contentType, ContentId = contentId, Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Comment)
            .WithName("AddCommunityContentComment")
            .WithSummary("Comment on any content type a module has claimed.");

        group.MapPost("/comments/{commentId:guid}/replies", async (
                Guid commentId,
                CreateCommentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new AddCommentCommand
                        {
                            Request = request with { ParentCommentId = commentId }
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Comment)
            .WithName("ReplyToCommunityComment")
            .WithSummary("Reply to a comment.");

        group.MapPut("/comments/{commentId:guid}", async (
                Guid commentId,
                UpdateCommentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCommentCommand { CommentId = commentId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ManageComment)
            .WithName("UpdateCommunityComment")
            .WithSummary("Edit your own comment.");

        group.MapDelete("/comments/{commentId:guid}", async (
                Guid commentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteCommentCommand { CommentId = commentId }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ManageComment)
            .WithName("DeleteCommunityComment")
            .WithSummary("Delete your own comment. The thread keeps its shape.");

        group.MapPut("/comments/{commentId:guid}/pin", async (
                Guid commentId,
                PinRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PinCommentCommand { CommentId = commentId, Pinned = request.Pinned },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Pin)
            .WithName("PinCommunityComment")
            .WithSummary("Pin a comment to the top of its thread.");
    }
}
