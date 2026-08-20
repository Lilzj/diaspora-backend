using Diaspora.Modules.Creator.Application.Creators;
using Diaspora.Modules.Creator.Application.Discovery;
using Diaspora.Modules.Creator.Application.Milestones;
using Diaspora.Modules.Creator.Application.Verification;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Creator.Api;

/// <summary>CREATOR-GAP-001. The creator projection.</summary>
internal static class CreatorProfileEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMyCreatorProfileQuery(), cancellationToken)))
            .WithName("GetMyCreatorProfile")
            .WithSummary(
                "The caller's creator projection, with the status the platform decided and what the "
                + "next rung needs.");

        group.MapPut("/me", async (
                UpdateCreatorProfileRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCreatorProfileCommand { Request = request }, cancellationToken)))
            .WithName("UpdateMyCreatorProfile")
            .WithSummary("Edits the creator-facing fields. Refused for a restricted creator.");
    }
}

/// <summary>CREATOR-GAP-002. The creator-programme verification.</summary>
internal static class CreatorVerificationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me/verification", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyCreatorVerificationQuery(), cancellationToken)))
            .WithName("GetMyCreatorVerification")
            .WithSummary("The caller's application, with every requirement decided by the server.");

        group.MapPost("/me/verification", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(new StartCreatorVerificationCommand(), cancellationToken)))
            .WithName("StartCreatorVerification")
            .WithSummary("Starts a draft. Refused unless the programme's requirements are met.");

        group.MapPost("/me/verification/evidence", async (
                AttachCreatorEvidenceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new AttachCreatorEvidenceCommand { Request = request }, cancellationToken)))
            .WithName("AttachCreatorEvidence")
            .WithSummary(
                "Attaches supporting material by reference. No file, no URI, no document number.");

        group.MapPost("/me/verification/submit", async (
                SubmitCreatorVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitCreatorVerificationCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("SubmitCreatorVerification")
            .WithSummary("Hands the application in. Idempotent on the key.");

        group.MapPost("/me/verification/appeal", async (
                AppealCreatorVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AppealCreatorVerificationCommand { Request = request },
                        cancellationToken)))
            .WithName("AppealCreatorVerification")
            .WithSummary("Appeals a refusal. Once.");

        // The reviewer's route. Needs creator:verification:review, which an applicant does not hold
        // — and a reviewer holding it is still refused their own application.
        group.MapPost("/verification/{applicationId:guid}/decide", async (
                Guid applicationId,
                DecideCreatorVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideCreatorVerificationCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideCreatorVerification")
            .WithSummary("Reviews, requests more, approves, refuses or suspends. Reviewer only.");
    }
}

/// <summary>CREATOR-GAP-003. Milestones.</summary>
internal static class CreatorMilestoneEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me/milestones", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMyMilestonesQuery(), cancellationToken)))
            .WithName("GetMyMilestones")
            .WithSummary(
                "The milestone catalogue with this creator's standing. Progress is weighted and "
                + "capped by the server.");

        group.MapPost("/me/milestones/{milestoneId:guid}/claim", async (
                Guid milestoneId,
                ClaimRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ClaimMilestoneCommand
                        {
                            MilestoneId = milestoneId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("ClaimMilestone")
            .WithSummary("Claims a completed milestone. Idempotent; a repeat never pays twice.");
    }
}

/// <summary>CREATOR-GAP-012, -014, and the public creator profile from -001.</summary>
internal static class CreatorDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // Literal before parameterised: /creators/leaderboard must not be read as a creator id.
        group.MapGet("/leaderboard", async (
                [FromQuery] string? scope,
                [FromQuery] string? period,
                [FromQuery] string? country,
                [FromQuery] Guid? communityId,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCreatorLeaderboardQuery
                        {
                            Scope = scope,
                            Period = period,
                            CountryCode = country,
                            CommunityId = communityId,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("GetCreatorLeaderboard")
            .WithSummary(
                "A ranking, with its score definition and the caller's own row even when it falls "
                + "off the page.");

        group.MapGet("/", async (
                [FromQuery] string? query,
                [FromQuery] string? country,
                [FromQuery] string? category,
                [FromQuery] string? expertise,
                [FromQuery] string? language,
                [FromQuery] bool? verified,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchCreatorsQuery
                        {
                            Query = query,
                            CountryCode = country,
                            Category = category,
                            Expertise = expertise,
                            Language = language,
                            Verified = verified,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("SearchCreators")
            .WithSummary(
                "The public creator directory, with facets. A creator who opted out is absent, not "
                + "filtered.");

        group.MapGet("/{creatorId:guid}", async (
                Guid creatorId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPublicCreatorQuery { CreatorId = creatorId }, cancellationToken)))
            .WithName("GetPublicCreator")
            .WithSummary("A creator's public projection. Private fields are absent rather than null.");
    }
}
