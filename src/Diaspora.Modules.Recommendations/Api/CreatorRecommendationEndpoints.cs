using Diaspora.Modules.Recommendations.Application.Creator;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// CREATOR-GAP-013: the creator rail and its dismissal.
/// <para>
/// <b>Two dismissal routes over one command.</b> <c>/creator/{recommendationId}/dismiss</c> is the
/// canonical one; <c>/{recommendationId}/dismiss</c> is the path the client has called since Phase 7
/// (<c>lib/creator-gap-api.ts</c>), served so connecting the gap needs no client change. Both have
/// two segments, so neither can collide with the one-segment <c>POST /dismiss</c> feedback verb,
/// which takes a feed target in its body and is a different thing. Literal two-segment routes —
/// <c>/profile/refresh</c>, <c>/preferences/blocks</c> — outrank the parameter by route precedence.
/// </para>
/// </summary>
internal static class CreatorRecommendationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/creator", async (
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCreatorRecommendationsQuery { Take = take }, cancellationToken)))
            .WithName("GetCreatorRecommendations")
            .WithSummary(
                "Ranked suggestions from the caller's own creator state: claimable milestones, "
                + "nearly finished challenges, grants they qualify for, courses in progress, "
                + "profile gaps and verification.");

        group.MapPost("/creator/{recommendationId}/dismiss", async (
                string recommendationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DismissCreatorRecommendationCommand { RecommendationId = recommendationId },
                        cancellationToken)))
            .WithName("DismissCreatorRecommendation")
            .WithSummary("Stops one creator suggestion appearing for the caller, on every device.");

        group.MapPost("/{recommendationId}/dismiss", async (
                string recommendationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DismissCreatorRecommendationCommand { RecommendationId = recommendationId },
                        cancellationToken)))
            .WithName("DismissRecommendationById")
            .WithSummary("The client's path for the same dismissal. See the creator route.");
    }
}
