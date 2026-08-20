using Diaspora.Modules.Jobs.Application.Dashboards;
using Diaspora.Modules.Jobs.Application.Employers;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>Registering and running an employer.</summary>
internal static class EmployerEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var employers = group.MapGroup("/employers");

        employers.MapPost("/", async (
                [FromBody] RegisterEmployerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RegisterEmployerCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(JobPolicies.EmployerCreate)
            .WithName("RegisterEmployer")
            .WithSummary("Register an employer profile, with you as its first owner.");

        employers.MapPut("/{employerId:guid}", async (
                Guid employerId,
                [FromBody] UpdateEmployerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateEmployerCommand
                        {
                            EmployerId = employerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.EmployerManage)
            .WithName("UpdateEmployer")
            .WithSummary("Update an employer profile.");

        // The employerId is optional: a recruiter who belongs to exactly one employer should not
        // have to name it, and one who belongs to several must.
        // ── The public profile, added to close gaps G-1 and G-2 ─────────────────────────────
        //
        // Every posting names an employer and nothing could open one: `GET /employers/{id}` was a
        // 404 and this path was a **405**, published for POST and PUT only. `AllowAnonymous`
        // because a job advert is public and the company behind it has to be readable by somebody
        // deciding whether to apply — `EmployerMapper` already withholds the legal name, the
        // registration number and the member list from anybody not on the team.
        //
        // `/employers/dashboard` is not shadowed by this: ASP.NET Core matches by segment
        // specificity rather than registration order, and a literal beats a parameter. Worth
        // stating because the registration order here suggests otherwise, and a reader checking
        // whether the dashboard still works should not have to rediscover the rule.
        employers.MapGet("/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetEmployerQuery { IdOrSlug = idOrSlug }, cancellationToken)))
            .AllowAnonymous()
            .WithName("GetEmployer")
            .WithSummary("An employer's public profile, by id or slug.");

        employers.MapGet("/dashboard", async (
                [FromQuery] Guid? employerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetEmployerDashboardQuery { EmployerId = employerId },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationsReview)
            .WithName("GetEmployerDashboard")
            .WithSummary("The recruiter's dashboard: who is waiting on you.");
    }
}
