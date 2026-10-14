using Diaspora.Modules.Housing.Application.Dashboards;
using Diaspora.Modules.Housing.Application.Neighborhoods;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// The neighbourhood surface.
/// <para>
/// <b>Every read here is anonymous, and that is most of what the module is for.</b> Somebody
/// deciding whether to move to a city needs to know what living in its neighbourhoods is like
/// before they commit to anything — before they join the platform, before they book a flight,
/// before they send anybody a deposit. Putting it behind a sign-in would make it useful only to
/// people who had already decided.
/// </para>
/// <para>
/// <b>The routes are addressed by Location's area identifier or by its slug</b>, not by any
/// identifier this module owns. A client that has an area from any other module already holds the
/// key, which is what makes the neighbourhood a shared platform concept rather than a Housing one.
/// </para>
/// </summary>
internal static class NeighborhoodEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapProfiles(group);
        MapInsights(group);
        MapAreaInsights(group);
    }

    /// <summary>
    /// Insights about the area around a point (14 Oct 2026, Phase 2A of retiring the place
    /// catalogue). The point-keyed successors of <c>/neighborhoods/insights</c>, which keep working
    /// for old app builds until Phase 3. Moderation (<c>decide</c>) and endorsement are shared: an
    /// insight is an insight, wherever it is.
    /// </summary>
    private static void MapAreaInsights(RouteGroupBuilder group)
    {
        group.MapPost("/area/insights", async (
                [FromBody] SubmitAreaInsightRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SubmitAreaInsightCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.InsightSubmit)
            .WithName("SubmitAreaInsight")
            .WithSummary("Share what you know about the area around a point. Moderated before it appears.");

        group.MapGet("/area/insights", async (
                [FromQuery] double latitude,
                [FromQuery] double longitude,
                [FromQuery] double? radiusKm,
                [FromQuery] string? category,
                [FromQuery] bool? mineOnly,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListAreaInsightsQuery
                        {
                            Latitude = latitude,
                            Longitude = longitude,
                            RadiusKm = radiusKm,
                            Category = category,
                            MineOnly = mineOnly ?? false,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListAreaInsights")
            .WithSummary("What members say about the area around a point. Approved only, unless your own.");
    }

    private static void MapProfiles(RouteGroupBuilder group)
    {
        group.MapGet("/neighborhoods", async (
                [FromQuery] string? countryCode,
                [FromQuery] Guid? areaId,
                [FromQuery] string? city,
                [FromQuery] string? query,
                [FromQuery] bool? withListingsOnly,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchNeighborhoodsQuery
                        {
                            CountryCode = countryCode,
                            AreaId = areaId,
                            City = city,
                            Query = query,
                            WithListingsOnly = withListingsOnly ?? false,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchNeighborhoods")
            .WithSummary("The neighbourhood directory.");

        group.MapGet("/neighborhoods/{idOrSlug}", async (
                string idOrSlug,
                [FromQuery] string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNeighborhoodProfileQuery
                        {
                            IdOrSlug = idOrSlug,
                            CountryCode = countryCode
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetNeighborhoodProfile")
            .WithSummary("What living in a neighbourhood is like.");

        group.MapPut("/neighborhoods", async (
                [FromBody] UpsertNeighborhoodProfileRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpsertNeighborhoodProfileCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.NeighborhoodManage)
            .WithName("UpsertNeighborhoodProfile")
            .WithSummary("Write or revise a neighbourhood profile. Lands unapproved.");

        group.MapPost("/neighborhoods/{areaId:guid}/approve", async (
                Guid areaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ApproveNeighborhoodProfileCommand { AreaId = areaId },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.NeighborhoodApprove)
            .WithName("ApproveNeighborhoodProfile")
            .WithSummary("Sign a neighbourhood profile off for publication.");

        group.MapGet("/neighborhoods/dashboard", async (
                [FromQuery] string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNeighborhoodDashboardQuery { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.NeighborhoodManage)
            .WithName("GetNeighborhoodDashboard")
            .WithSummary("What is unwritten, unreviewed and waiting.");
    }

    private static void MapInsights(RouteGroupBuilder group)
    {
        group.MapPost("/neighborhoods/insights", async (
                [FromBody] SubmitNeighborhoodInsightRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SubmitNeighborhoodInsightCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.InsightSubmit)
            .WithName("SubmitNeighborhoodInsight")
            .WithSummary("Share what you know about a neighbourhood. Moderated before it appears.");

        group.MapGet("/neighborhoods/insights", async (
                [FromQuery] Guid? areaId,
                [FromQuery] string? category,
                [FromQuery] bool? pendingOnly,
                [FromQuery] bool? mineOnly,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListNeighborhoodInsightsQuery
                        {
                            AreaId = areaId,
                            Category = category,
                            PendingOnly = pendingOnly ?? false,
                            MineOnly = mineOnly ?? false,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListNeighborhoodInsights")
            .WithSummary("What members say about a place. Approved only, unless you may see more.");

        group.MapPost("/neighborhoods/insights/{insightId:guid}/decide", async (
                Guid insightId,
                [FromBody] DecideNeighborhoodInsightRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideNeighborhoodInsightCommand
                        {
                            InsightId = insightId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.InsightModerate)
            .WithName("DecideNeighborhoodInsight")
            .WithSummary("Approve, reject or retire an insight.");

        group.MapPost("/neighborhoods/insights/{insightId:guid}/endorse", async (
                Guid insightId,
                [FromBody] EndorseInsightRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new EndorseNeighborhoodInsightCommand
                        {
                            InsightId = insightId,
                            Request = request ?? new EndorseInsightRequest()
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.InsightEndorse)
            .WithName("EndorseNeighborhoodInsight")
            .WithSummary("Agree, or withdraw agreement, with an insight.");
    }
}
