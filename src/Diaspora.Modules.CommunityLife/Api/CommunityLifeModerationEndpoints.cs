using Diaspora.Modules.CommunityLife.Application.Moderation;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// Reporting and the moderation queue.
/// <para>
/// <b>The subject type is in the route rather than the body.</b> Reporting an event and reporting an
/// organization are the same act with the same shape, and a client that had to know which command to
/// send would eventually send the wrong one — while a URL that names the subject is one a moderator
/// can read in a log.
/// </para>
/// </summary>
internal static class CommunityLifeModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/{subjectType}/{subjectId:guid}/reports", async (
                string subjectType,
                Guid subjectId,
                ReportSubjectRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ReportSubjectCommand
                        {
                            SubjectType = ToSubjectType(subjectType),
                            SubjectId = subjectId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("ReportCommunityLifeSubject")
            .WithSummary("Reports an event or an organization.");

        group.MapPost("/{subjectType}/{subjectId:guid}/moderation", async (
                string subjectType,
                Guid subjectId,
                ModerateSubjectRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ModerateSubjectCommand
                        {
                            SubjectType = ToSubjectType(subjectType),
                            SubjectId = subjectId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("ModerateCommunityLifeSubject")
            .WithSummary("Hides, suspends or clears a subject. Staff only.");

        group.MapGet("/reports", async (
                [FromQuery] string? subjectType,
                [FromQuery(Name = "status")] string[]? statuses,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListReportsQuery
                        {
                            SubjectType = subjectType,
                            Statuses = statuses,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListCommunityLifeReports")
            .WithSummary("The moderation queue. Staff only.");

        group.MapPost("/reports/{reportId:guid}/resolution", async (
                Guid reportId,
                ResolveReportRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ResolveReportCommand { ReportId = reportId, Request = request },
                        cancellationToken)))
            .WithName("ResolveCommunityLifeReport")
            .WithSummary("Closes a report, optionally acting on its subject. Staff only.");
    }

    /// <summary>
    /// The plural route segment, singularised into the subject type the command expects.
    /// <para>
    /// Anything unrecognised is passed through unchanged and refused by the handler, so a mistyped
    /// segment produces a validation failure naming the problem rather than a route that silently
    /// reports the wrong kind of thing.
    /// </para>
    /// </summary>
    private static string ToSubjectType(string segment) => segment switch
    {
        "events" => "Event",
        "organizations" => "Organization",
        _ => segment
    };
}
