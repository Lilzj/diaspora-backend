using Diaspora.Modules.Recommendations.Application.Feed;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// The member's own recommendations.
/// <para>
/// <b>Every route here is implicitly scoped to the caller; none takes a user identifier.</b> That is
/// what makes reading somebody else's recommendations unexpressible rather than merely forbidden —
/// the same property B13 gave the inbox, and for the same reason.
/// </para>
/// <para>
/// <b>The surface is a route segment rather than a query parameter</b> — <c>/jobs</c>,
/// <c>/housing</c>, <c>/guides</c> — because the client already thinks in screens and because a
/// segment is cacheable, linkable and readable in a log. The generic <c>/surface/{surface}</c> route
/// exists beneath them so a surface added to the catalogue is reachable before anybody adds a named
/// route for it.
/// </para>
/// </summary>
internal static class FeedEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/feed", async (
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetHomeFeedQuery { CountryCode = country }, cancellationToken)))
            .WithName("GetHomeFeed")
            .WithSummary("The caller's home feed, as sections.");

        // The named surfaces the client routes to today.
        MapSurface(group, "jobs", "Jobs", "Job");
        MapSurface(group, "housing", "Housing", "HousingListing");
        MapSurface(group, "marketplace", "Marketplace", "MarketplaceListing");
        MapSurface(group, "providers", "Providers", "Provider");
        MapSurface(group, "guides", "Guides", "Guide");
        MapSurface(group, "learning-paths", "LearningPaths", "LearningPath");
        MapSurface(group, "events", "Events", "Event");
        MapSurface(group, "organizations", "Organizations", "Organization");
        MapSurface(group, "neighborhoods", "Neighborhoods", "Neighborhood");
        MapSurface(group, "communities", "Communities", "Community");

        group.MapGet("/trending", async (
                [FromQuery] string? type,
                [FromQuery] string? country,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationsQuery
                        {
                            Surface = "Trending",
                            TargetType = type,
                            CountryCode = country,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("GetTrending")
            .WithSummary("What is moving right now. Deliberately barely personalised.");

        group.MapGet("/continue-learning", async (
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationsQuery
                        {
                            Surface = "ContinueLearning",
                            TargetType = "LearningPath",
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("GetContinueLearning")
            .WithSummary("Where the caller left off.");

        group.MapGet("/explore", async (
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationsQuery { Surface = "Explore", Limit = limit },
                        cancellationToken)))
            .WithName("GetExploreRecommendations")
            .WithSummary("Explore Diaspora's recommended tutorials and guides.");

        // The escape hatch. A surface configured in the catalogue is reachable here before anybody
        // adds a named route for it — see the class summary.
        group.MapGet("/surface/{surface}", async (
                string surface,
                [FromQuery] string? type,
                [FromQuery] string? country,
                [FromQuery(Name = "category")] string[]? categories,
                [FromQuery] int? limit,
                [FromQuery] bool? refresh,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationsQuery
                        {
                            Surface = surface,
                            TargetType = type,
                            CountryCode = country,
                            Categories = categories ?? [],
                            Limit = limit,
                            Refresh = refresh ?? false
                        },
                        cancellationToken)))
            .WithName("GetRecommendationsForSurface")
            .WithSummary("Recommendations for any configured surface.");
    }

    /// <summary>
    /// Maps one named surface.
    /// <para>
    /// The target type is bound to the route rather than left to the caller, because
    /// <c>/recommendations/jobs</c> returning a guide would be a surprise no client should have to
    /// defend against.
    /// </para>
    /// </summary>
    private static void MapSurface(
        RouteGroupBuilder group, string route, string surface, string targetType) =>
        group.MapGet($"/{route}", async (
                [FromQuery] string? country,
                [FromQuery(Name = "category")] string[]? categories,
                [FromQuery] int? limit,
                [FromQuery] bool? refresh,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRecommendationsQuery
                        {
                            Surface = surface,
                            TargetType = targetType,
                            CountryCode = country,
                            Categories = categories ?? [],
                            Limit = limit,
                            Refresh = refresh ?? false
                        },
                        cancellationToken)))
            .WithName($"Get{surface}Recommendations")
            .WithSummary($"Recommended {route.Replace('-', ' ')} for the caller.");
}
