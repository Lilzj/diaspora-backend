using Diaspora.Modules.Recommendations.Application.Communities;
using Diaspora.Modules.Recommendations.Application.Feed;
using Diaspora.Modules.Recommendations.Application.Monetization;
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

        // Communities and monetization rank from facts other modules own, not from the engine's
        // pool. Each is also mapped under `/surface/{name}` as a literal, which outranks the generic
        // route below, so the escape hatch cannot answer them with the engine's empty page.
        MapCommunities(group, "/communities", "GetCommunitiesRecommendations");
        MapCommunities(group, "/surface/communities", "GetCommunitiesRecommendationsForSurface");
        MapMonetization(group, "/monetization", "GetMonetizationRecommendations");
        MapMonetization(group, "/surface/monetization", "GetMonetizationRecommendationsForSurface");

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
    /// Communities to suggest, filtered by presence country.
    /// <para>
    /// Binds the same parameters the engine-backed route bound — <c>country</c>, <c>category</c>,
    /// <c>limit</c>, <c>refresh</c> — so no client changes. <c>refresh</c> is accepted and has
    /// nothing to do: every request ranks afresh.
    /// </para>
    /// </summary>
    private static void MapCommunities(RouteGroupBuilder group, string route, string name) =>
        group.MapGet(route, async (
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
                        new GetCommunityRecommendationsQuery
                        {
                            CountryCode = country,
                            Categories = categories ?? [],
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName(name)
            .WithSummary(
                "Communities the caller could join that are present in a country — `country`, else "
                + "the caller's own — never one they belong to or dismissed.");

    /// <summary>
    /// MON-GAP-011: plans and promotions the caller could buy.
    /// <para>
    /// The client also sends <c>surface</c> (where the rail is drawn); it keys the client's cache
    /// and does not narrow the answer, so it is not bound.
    /// </para>
    /// </summary>
    private static void MapMonetization(RouteGroupBuilder group, string route, string name) =>
        group.MapGet(route, async (
                [FromQuery] string? country,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMonetizationRecommendationsQuery { CountryCode = country, Limit = limit },
                        cancellationToken)))
            .WithName(name)
            .WithSummary(
                "Plans and promotions the caller could buy, ranked from their own listings, plan "
                + "and purchases. Never one they hold; dismissed through POST /recommendations/dismiss.");

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
