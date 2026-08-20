using Diaspora.Modules.Recommendations.Application.Analytics;
using Diaspora.Modules.Recommendations.Application.Policies;
using Diaspora.Modules.Recommendations.Application.Strategies;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// What decides everybody else's experience.
/// <para>
/// <b>Grouped under <c>/operations</c> and staff-only, because the line this module draws is
/// "whose experience does this change".</b> Everything above is the caller's own; everything here
/// moves what a whole country sees, and the separation is visible in the URL rather than only in a
/// permission table.
/// </para>
/// </summary>
internal static class RecommendationOperationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var operations = group.MapGroup("/operations");

        operations.MapGet("/strategies", async (
                [FromQuery] string? surface,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRankingStrategiesQuery { Surface = surface }, cancellationToken)))
            .WithName("GetRankingStrategies")
            .WithSummary("The configured ranking weights, with whether anything implements each key.");

        operations.MapPut("/strategies/{id:guid}", async (
                Guid id,
                UpdateRankingStrategyRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateRankingStrategyCommand
                        {
                            StrategyId = id,
                            Weight = request.Weight,
                            Order = request.Order,
                            IsEnabled = request.IsEnabled,
                            RunsOnColdStart = request.RunsOnColdStart,
                            MinimumScore = request.MinimumScore,
                            Description = request.Description,
                            Parameters = request.Parameters
                        },
                        cancellationToken)))
            .WithName("UpdateRankingStrategy")
            .WithSummary("Retunes one surface's ranking. Audited, and takes effect immediately.");

        operations.MapGet("/policies", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetRecommendationPoliciesQuery(), cancellationToken)))
            .WithName("GetRecommendationPolicies")
            .WithSummary("Every policy, enabled or not, with whether anything implements each key.");

        operations.MapPut("/policies/{id:guid}", async (
                Guid id,
                UpdateRecommendationPolicyRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateRecommendationPolicyCommand
                        {
                            PolicyId = id,
                            IsEnabled = request.IsEnabled,
                            Order = request.Order,
                            DemotionFactor = request.DemotionFactor,
                            Description = request.Description,
                            Surfaces = request.Surfaces,
                            TargetTypes = request.TargetTypes,
                            Parameters = request.Parameters
                        },
                        cancellationToken)))
            .WithName("UpdateRecommendationPolicy")
            .WithSummary("Changes one policy. A system policy refuses to be disabled.");

        operations.MapGet("/analytics", async (
                [FromQuery] string? surface,
                [FromQuery] DateOnly? from,
                [FromQuery] DateOnly? to,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationAnalyticsQuery
                        {
                            Surface = surface,
                            From = from,
                            To = to,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("GetRecommendationAnalytics")
            .WithSummary("How the recommendations are performing. Aggregate, never per-member.");
    }
}
