using Diaspora.Modules.Jobs.Application.Candidates;
using Diaspora.Modules.Jobs.Application.Dashboards;
using Diaspora.Modules.Jobs.Application.Saved;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// The candidate's own surfaces: their profile, their saved jobs, their dashboard.
/// <para>
/// Every route here operates on the caller's own data and takes no candidate identifier. That is
/// deliberate: an endpoint that accepted one would be an endpoint somebody could point at
/// somebody else, and the ownership check would be the only thing standing between a guessable
/// identifier and a stranger's visa status.
/// </para>
/// </summary>
internal static class CandidateEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var candidates = group.MapGroup("/candidate");

        // ── The read, added to close gap G-5 ────────────────────────────────────────────────
        //
        // `POST` and `PUT` on this path both existed and `GET` answered **405**: a profile a
        // member could create and update but never read back, so the edit screen had nothing to
        // populate from and every save was a blind overwrite.
        candidates.MapGet("/profile", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyCandidateProfileQuery(), cancellationToken)))
            .RequireAuthorization(JobPolicies.CandidateManage)
            .WithName("GetMyCandidateProfile")
            .WithSummary("Your own candidate profile.");

        candidates.MapPost("/profile", async (
                [FromBody] CreateCandidateProfileRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateCandidateProfileCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.CandidateManage)
            .WithName("CreateCandidateProfile")
            .WithSummary("Create your professional profile. Created private.");

        candidates.MapPut("/profile", async (
                [FromBody] UpdateCandidateProfileRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCandidateProfileCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.CandidateManage)
            .WithName("UpdateCandidateProfile")
            .WithSummary("Update your professional profile.");

        // Its own route, because it is the one setting somebody changes urgently and on its own.
        candidates.MapPut("/profile/visibility", async (
                [FromBody] ChangeCandidateVisibilityCommand command,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(context, await sender.Send(command, cancellationToken)))
            .RequireAuthorization(JobPolicies.CandidateManage)
            .WithName("ChangeCandidateVisibility")
            .WithSummary("Change who can see your profile.");

        candidates.MapGet("/dashboard", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetCandidateDashboardQuery(), cancellationToken)))
            .RequireAuthorization(JobPolicies.SavedManage)
            .WithName("GetCandidateDashboard")
            .WithSummary("The job seeker's dashboard: offers, interviews, applications.");

        // ── Saved jobs ──────────────────────────────────────────────────────────────────────

        group.MapPut("/{jobId:guid}/save", async (
                Guid jobId,
                [FromBody] SaveJobRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SaveJobCommand { JobId = jobId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.SavedManage)
            .WithName("SaveJob")
            .WithSummary("Save a job to come back to. Idempotent.");

        group.MapDelete("/{jobId:guid}/save", async (
                Guid jobId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UnsaveJobCommand { JobId = jobId }, cancellationToken)))
            .RequireAuthorization(JobPolicies.SavedManage)
            .WithName("UnsaveJob")
            .WithSummary("Remove a job from your saved list. Idempotent.");
    }
}
