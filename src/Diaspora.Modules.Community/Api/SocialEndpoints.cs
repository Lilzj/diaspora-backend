using Diaspora.Modules.Community.Application.Follows;
using Diaspora.Modules.Community.Application.Shares;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>Shares and follows.</summary>
internal static class SocialEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/content/{contentType}/{contentId:guid}/shares", async (
                string contentType,
                Guid contentId,
                CreateShareRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateShareCommand
                        {
                            ContentType = contentType, ContentId = contentId, Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Share)
            .WithName("ShareCommunityContent")
            .WithSummary("Record a share. External destinations are self-declared.");

        group.MapPost("/posts/{postId:guid}/shares", async (
                Guid postId,
                CreateShareRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateShareCommand
                        {
                            ContentType = "post", ContentId = postId, Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Share)
            .WithName("ShareCommunityPost")
            .WithSummary("Record a share of a post.");

        group.MapGet("/follows/{targetId:guid}", async (
                Guid targetId,
                string? targetType,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetFollowStateQuery { TargetId = targetId, TargetType = targetType },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Follow)
            .WithName("GetCommunityFollowState")
            .WithSummary("Whether the caller follows a target, and how many people do.");

        group.MapPost("/follows/{targetId:guid}", async (
                Guid targetId,
                FollowRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetFollowCommand
                        {
                            TargetId = targetId,
                            TargetType = request?.TargetType,
                            Follow = true,
                            Muted = request?.Muted ?? false
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Follow)
            .WithName("FollowCommunityTarget")
            .WithSummary("Follow a member, an organization or a hashtag.");

        group.MapDelete("/follows/{targetId:guid}", async (
                Guid targetId,
                string? targetType,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetFollowCommand
                        {
                            TargetId = targetId, TargetType = targetType, Follow = false
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Follow)
            .WithName("UnfollowCommunityTarget")
            .WithSummary("Unfollow.");
    }
}
