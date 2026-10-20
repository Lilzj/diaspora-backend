using Diaspora.Modules.Creator.Application.Activity;
using Diaspora.Modules.Creator.Application.Creators;
using Diaspora.Modules.Creator.Application.Discovery;
using Diaspora.Modules.Creator.Application.Milestones;
using Diaspora.Modules.Creator.Application.Verification;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Platform.Http;
using Diaspora.SharedKernel.Results;
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

        // BG-08. The rubric, not a score — see ContentHealthRubric for why the evaluation cannot
        // live here without giving Creator a dependency on four content modules. Anonymous-safe:
        // it is the platform's definition of the word, not anybody's data.
        group.MapGet("/content-health/rubric", (HttpContext context) =>
                PlatformResults.Ok(context, Result.Success(ContentHealthRubricResponse.Current)))
            .AllowAnonymous()
            .WithName("GetContentHealthRubric")
            .WithSummary(
                "What content health means: the criteria, their weights and the thresholds. The "
                + "client applies this to the items it already holds.");

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

        // The read the workspace timeline was waiting for. `CreatorActivityEntry` has been written
        // on every recognised action since the module shipped and nothing served it back.
        // `cursor` and `limit`, which is what the client has always sent. The route bound
        // `before` and `take`, so neither arrived and every request was answered as page one.
        group.MapGet("/me/activity", async (
                string? cursor,
                int? limit,
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyCreatorActivityQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .WithName("GetMyCreatorActivity")
            .WithSummary("The caller's own recognised activity, newest first.");

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

        // Literal segment, so it cannot be read as a creator id.
        group.MapGet("/by-user/{userId:guid}", async (
                Guid userId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPublicCreatorByUserQuery { UserId = userId }, cancellationToken)))
            .WithName("GetPublicCreatorByUser")
            .WithSummary(
                "A creator's public projection, by the member behind it. Reached from a byline, so "
                + "directory opt-out does not hide it.");

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
