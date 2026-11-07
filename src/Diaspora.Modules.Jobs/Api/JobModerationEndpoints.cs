using Diaspora.Modules.Jobs.Application.Moderation;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// Reporting a job advert, and what a moderator does about it.
/// <para>
/// <b>Every other vertical has had this surface and jobs did not.</b> Housing, marketplace,
/// guides, community and community life could all be reported and moderated; a job advert could
/// not. <c>jobs:report</c> and <c>jobs:moderate</c> were declared and gated no route, and the
/// posting aggregate has carried <c>Hide</c>, <c>Flag</c> and its moderation trail since it was
/// written with nothing able to reach them.
/// </para>
/// <para>
/// That mattered more here than it would have anywhere else. Recruitment fraud targets people
/// who have just arrived: an advert that asks a candidate to pay for a visa, a course or a
/// background check takes money from somebody with no income yet and nobody to ask whether that
/// is normal. Until these routes existed, a member who spotted one had no way to say so and
/// staff had no way to take it down.
/// </para>
/// </summary>
internal static class JobModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/postings/{postingId:guid}/reports", async (
                Guid postingId,
                ReportJobPostingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ReportJobPostingCommand { PostingId = postingId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.Report)
            .WithName("ReportJobPosting")
            .WithSummary(
                "Report a job advert. A scam or a demand for money flags it on the first report.");

        group.MapGet("/moderation/reports", async (
                [FromQuery] bool? openOnly,
                [FromQuery] bool? fraudFirst,
                [FromQuery] Guid? postingId,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListJobReportsQuery
                        {
                            OpenOnly = openOnly ?? true,
                            FraudFirst = fraudFirst ?? true,
                            PostingId = postingId,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.Moderate)
            .WithName("ListJobReports")
            .WithSummary("The jobs moderation queue. The reports about money come first.");

        // Resolving a report and hiding an advert were actions here until 7 Nov 2026. They now happen
        // only as decisions on the Trust moderation case (/trust/moderation/queue/{id}/decision),
        // which carries the decision out through JobModerationTarget and closes these reports.
    }
}
