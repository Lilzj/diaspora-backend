using Diaspora.Modules.Jobs.Application.Applications;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// Applying, and working the applications that arrive.
/// <para>
/// <b>Withdrawal is its own route with its own policy, and that separation is deliberate.</b> It
/// is the candidate's decision, granted under restriction, and it must not be reachable from the
/// employer's decision endpoint by passing a status value.
/// </para>
/// </summary>
internal static class JobApplicationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/{jobId:guid}/apply", async (
                Guid jobId,
                [FromBody] ApplyToJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ApplyToJobCommand { JobId = jobId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.Apply)
            .WithName("ApplyToJob")
            .WithSummary("Apply to a job.");

        var applications = group.MapGroup("/applications");

        applications.MapGet("/", async (
                [AsParameters] ApplicationListParameters parameters,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(parameters.ToQuery(), cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationsReview)
            .WithName("ListApplications")
            .WithSummary("The employer's applicant list, or your own tracker.");

        // The candidate's exit. Its own policy, granted under restriction.
        applications.MapPost("/{applicationId:guid}/withdraw", async (
                Guid applicationId,
                [FromBody] WithdrawApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawApplicationCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationWithdraw)
            .WithName("WithdrawApplication")
            .WithSummary("Withdraw your own application.");

        // The employer's decision. Cannot reach Withdrawn.
        applications.MapPost("/{applicationId:guid}/decide", async (
                Guid applicationId,
                [FromBody] DecideApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideApplicationCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationsDecide)
            .WithName("DecideApplication")
            .WithSummary("Move an application through the hiring workflow.");
    }
}
