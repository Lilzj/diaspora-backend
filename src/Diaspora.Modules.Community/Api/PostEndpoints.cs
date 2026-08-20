using Diaspora.Modules.Community.Application.Polls;
using Diaspora.Modules.Community.Application.Posts;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>Posts, their revisions, their pinning and their polls.</summary>
internal static class PostEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/posts", async (
                CreatePostRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(new CreatePostCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Post)
            .WithName("CreateCommunityPost")
            .WithSummary("Create a post, as a draft or published.");

        group.MapGet("/posts/{postId:guid}", async (
                Guid postId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetPostQuery { PostId = postId }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("GetCommunityPost")
            .WithSummary("Read a post, if its audience includes you.");

        group.MapPut("/posts/{postId:guid}", async (
                Guid postId,
                UpdatePostRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdatePostCommand { PostId = postId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ManagePost)
            .WithName("UpdateCommunityPost")
            .WithSummary("Edit your own post. Captures a revision first.");

        group.MapDelete("/posts/{postId:guid}", async (
                Guid postId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new DeletePostCommand { PostId = postId }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ManagePost)
            .WithName("DeleteCommunityPost")
            .WithSummary("Delete your own post.");

        group.MapGet("/posts/{postId:guid}/revisions", async (
                Guid postId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListPostRevisionsQuery { PostId = postId }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("ListCommunityPostRevisions")
            .WithSummary("The edit history of a post, newest first.");

        group.MapPut("/posts/{postId:guid}/pin", async (
                Guid postId,
                PinRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PinPostCommand { PostId = postId, Pinned = request.Pinned },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Pin)
            .WithName("PinCommunityPost")
            .WithSummary("Pin or unpin a post. A pinned post keeps its status.");

        group.MapPost("/posts/{postId:guid}/poll/votes", async (
                Guid postId,
                CastVoteRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CastVoteCommand { PostId = postId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Poll)
            .WithName("CastCommunityPollVote")
            .WithSummary("Vote in a poll. Re-voting replaces your previous answer.");
    }
}
