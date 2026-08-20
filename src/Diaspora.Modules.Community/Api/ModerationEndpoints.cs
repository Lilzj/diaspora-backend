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

        group.MapPost("/moderation/decisions", async (
                ModerationDecisionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new DecideModerationCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Moderate)
            .WithName("DecideCommunityModeration")
            .WithSummary("Decide a case. Recorded append-only and published.");

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
