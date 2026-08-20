using Diaspora.Modules.CommunityProgramme.Application.Announcements;
using Diaspora.Modules.CommunityProgramme.Application.Insights;
using Diaspora.Modules.CommunityProgramme.Application.Polls;
using Diaspora.Modules.CommunityProgramme.Application.Search;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>COMMUNITY-LIFE-GAP-012: polls, voting and server-computed results.</summary>
internal static class PollEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/polls", async (
                [FromQuery] Guid? organizationId,
                [FromQuery] string? audience,
                [FromQuery] string? state,
                [FromQuery] string? query,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListPollsQuery
                        {
                            OrganizationId = organizationId,
                            Audience = audience,
                            State = state,
                            Query = query,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListCommunityPolls")
            .WithSummary("Polls the caller is inside the audience of.");

        group.MapPost("/polls", async (
                CreatePollRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreatePollCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("CreateCommunityPoll")
            .WithSummary("Creates a poll. Role-gated on an organization poll.");

        group.MapGet("/polls/{pollId:guid}", async (
                Guid pollId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetPollQuery { PollId = pollId }, cancellationToken)))
            .WithName("GetCommunityPoll")
            .WithSummary("One poll, with the caller's own participation. 403 outside the audience.");

        group.MapGet("/polls/{pollId:guid}/results", async (
                Guid pollId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPollResultQuery { PollId = pollId }, cancellationToken)))
            .WithName("GetCommunityPollResult")
            .WithSummary("The tally, computed in the database. Null options while withheld.");

        group.MapPost("/polls/{pollId:guid}/responses", async (
                Guid pollId,
                SubmitPollResponseRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitPollResponseCommand
                        {
                            PollId = pollId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("SubmitCommunityPollResponse")
            .WithSummary("Casts or changes a vote. Answers with the result, not the vote.");

        // Mapped after /results and /responses so those literals are not caught by {action}.
        group.MapPost("/polls/{pollId:guid}/{action}", async (
                Guid pollId,
                string action,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ActOnPollCommand { PollId = pollId, Action = action },
                        cancellationToken)))
            .WithName("ActOnCommunityPoll")
            .WithSummary("Publishes, closes, cancels or publishes the results of a poll.");
    }
}

/// <summary>COMMUNITY-LIFE-GAP-016: scoped announcements.</summary>
internal static class AnnouncementEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/announcements", async (
                [FromQuery] string? scope,
                [FromQuery] Guid? scopeId,
                [FromQuery] string? countryCode,
                [FromQuery] string? query,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListAnnouncementsQuery
                        {
                            Scope = scope,
                            ScopeId = scopeId,
                            CountryCode = countryCode,
                            Query = query,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListCommunityAnnouncements")
            .WithSummary("The announcement feed, with the audience rule applied in the query.");

        group.MapPost("/announcements", async (
                PublishAnnouncementRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new PublishAnnouncementCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("PublishCommunityAnnouncement")
            .WithSummary("Publishes a scoped announcement. Role-gated; B13 delivers it.");
    }
}

/// <summary>COMMUNITY-LIFE-GAP-014, -017 and -021: analytics, impact and federated search.</summary>
internal static class InsightEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/organizations/{organizationId:guid}/analytics", async (
                Guid organizationId,
                [FromQuery] string? period,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOrganizationAnalyticsQuery
                        {
                            OrganizationId = organizationId,
                            Period = period
                        },
                        cancellationToken)))
            .WithName("GetOrganizationProgrammeAnalytics")
            .WithSummary("An organization's programme analytics. Computed on read.");

        group.MapGet("/organizations/{organizationId:guid}/analytics/members", async (
                Guid organizationId,
                [FromQuery] string? period,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMemberAnalyticsQuery
                        {
                            OrganizationId = organizationId,
                            Period = period
                        },
                        cancellationToken)))
            .WithName("GetOrganizationMemberAnalytics")
            .WithSummary("An organization's membership analytics, broken down by state.");

        group.MapGet("/impact", async (
                [FromQuery] string? scope,
                [FromQuery] Guid? scopeId,
                [FromQuery] string? period,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityImpactQuery
                        {
                            Scope = scope,
                            ScopeId = scopeId,
                            Period = period
                        },
                        cancellationToken)))
            .WithName("GetCommunityImpact")
            .WithSummary("Community impact. Every metric carries its own definition.");

        group.MapGet("/search", async (
                [FromQuery(Name = "q")] string? query,
                [FromQuery(Name = "types")] string[]? types,
                [FromQuery] string? countryCode,
                [FromQuery] string? cityName,
                [FromQuery] Guid? organizationId,
                [FromQuery] string? cause,
                [FromQuery] string? priceBand,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchCommunityQuery
                        {
                            Query = query,
                            Types = types,
                            CountryCode = countryCode,
                            CityName = cityName,
                            OrganizationId = organizationId,
                            Cause = cause,
                            PriceBand = priceBand,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("SearchCommunityProgramme")
            .WithSummary("The federated search over what B18 owns. Ordered by server relevance.");
    }
}
