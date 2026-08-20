using Diaspora.Modules.Community.Application.Reactions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>
/// Reactions.
/// <para>
/// POST adds and DELETE withdraws, on the same path, which is what an optimistic toggle needs.
/// The response carries the new counts so a client reconciles without refetching the card.
/// </para>
/// </summary>
internal static class ReactionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/content/{contentType}/{contentId:guid}/reactions", async (
                string contentType,
                Guid contentId,
                ReactRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReactCommand
                        {
                            ContentType = contentType,
                            ContentId = contentId,
                            ReactionKey = request.ReactionKey,
                            Add = true
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.React)
            .WithName("AddCommunityReaction")
            .WithSummary("React to anything. The reaction key comes from the catalogue.");

        group.MapDelete("/content/{contentType}/{contentId:guid}/reactions/{reactionKey}", async (
                string contentType,
                Guid contentId,
                string reactionKey,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReactCommand
                        {
                            ContentType = contentType,
                            ContentId = contentId,
                            ReactionKey = reactionKey,
                            Add = false
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.React)
            .WithName("RemoveCommunityReaction")
            .WithSummary("Withdraw a reaction.");

        group.MapPost("/posts/{postId:guid}/reactions", async (
                Guid postId,
                ReactRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReactCommand
                        {
                            ContentType = "post",
                            ContentId = postId,
                            ReactionKey = request.ReactionKey,
                            Add = true
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.React)
            .WithName("AddCommunityPostReaction")
            .WithSummary("React to a post. The confirm control on an alert lands here.");

        group.MapDelete("/posts/{postId:guid}/reactions/{reactionKey}", async (
                Guid postId,
                string reactionKey,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReactCommand
                        {
                            ContentType = "post",
                            ContentId = postId,
                            ReactionKey = reactionKey,
                            Add = false
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.React)
            .WithName("RemoveCommunityPostReaction")
            .WithSummary("Withdraw a reaction from a post.");
    }
}
