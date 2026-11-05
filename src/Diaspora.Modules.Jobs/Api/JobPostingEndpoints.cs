using Diaspora.Modules.Jobs.Application.Postings;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// Writing a posting and moving it through its life.
/// <para>
/// Every route here names a policy, and every handler behind it additionally asks the employer
/// aggregate whether the caller may act on <em>this</em> posting. The policy is a necessary
/// condition, never a sufficient one — see the permission matrix.
/// </para>
/// <para>
/// The lifecycle verbs are separate routes rather than a status field on the update, because
/// publishing, closing and archiving are decisions with their own permissions and their own
/// consequences, and a PUT that could do any of them by accident is one that eventually does.
/// </para>
/// </summary>
internal static class JobPostingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/", async (
                [FromBody] CreateJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateJobCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(JobPolicies.PostCreate)
            .WithName("CreateJob")
            .WithSummary("Draft a job posting.");

        group.MapPut("/{jobId:guid}", async (
                Guid jobId,
                [FromBody] UpdateJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateJobCommand { JobId = jobId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.PostEditOwn)
            .WithName("UpdateJob")
            .WithSummary("Update a job posting.");

        group.MapPost("/{jobId:guid}/publish", async (
                Guid jobId,
                [FromBody] PublishJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishJobCommand { JobId = jobId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.PostPublish)
            .WithName("PublishJob")
            .WithSummary("Publish a job posting, opening it for applications.");

        group.MapPost("/{jobId:guid}/close", async (
                Guid jobId,
                [FromBody] CloseJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CloseJobCommand { JobId = jobId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.PostClose)
            .WithName("CloseJob")
            .WithSummary("Close a job posting to applications.");

        group.MapPost("/{jobId:guid}/places/fill", async (
                Guid jobId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new FillJobPlaceCommand { JobId = jobId }, cancellationToken)))
            .RequireAuthorization(JobPolicies.PostClose)
            .WithName("FillJobPlace")
            .WithSummary("Count one place on a job as taken; the last one fills the job.");

        group.MapPost("/{jobId:guid}/archive", async (
                Guid jobId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveJobCommand { JobId = jobId }, cancellationToken)))
            .RequireAuthorization(JobPolicies.PostArchive)
            .WithName("ArchiveJob")
            .WithSummary("Retire a job posting from every public surface.");
    }
}
