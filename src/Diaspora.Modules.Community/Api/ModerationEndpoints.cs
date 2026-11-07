using Diaspora.Modules.Community.Application.Moderation;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>Reporting and moderation.</summary>
internal static class ModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/reports", async (
                CreateReportRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ReportContentCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Report)
            .WithName("ReportCommunityContent")
            .WithSummary("Report content. Accepts both of the client's reason taxonomies.");

        group.MapGet("/reports/reasons", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListReportReasonsQuery(), cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("ListCommunityReportReasons")
            .WithSummary("The reason catalogue a report modal should be built from.");

        group.MapGet("/moderation/queue", async (
                string? contentType,
                string? reason,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetModerationQueueQuery
                        {
                            ContentType = contentType,
                            Reason = reason,
                            Cursor = cursor,
                            Limit = limit ?? CommunityLimits.DefaultFeedPageSize
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ModerationRead)
            .WithName("GetCommunityModerationQueue")
            .WithSummary("Open reports, grouped by the content they are about.");

        // POST /moderation/decisions is gone (7 Nov 2026). It let staff act on any module's content
        // with the report and the reason optional, and no notice or appeal. Staff decide on the
        // Trust moderation case now (/trust/moderation/...), which every report here reaches.

        group.MapGet("/moderation/history/{contentType}/{contentId:guid}", async (
                string contentType,
                Guid contentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetModerationHistoryQuery
                        {
                            ContentType = contentType, ContentId = contentId
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.ModerationRead)
            .WithName("GetCommunityModerationHistory")
            .WithSummary("Every decision ever made about a piece of content.");
    }
}
