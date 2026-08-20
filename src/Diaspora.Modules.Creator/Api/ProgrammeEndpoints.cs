using Diaspora.Modules.Creator.Application.Academy;
using Diaspora.Modules.Creator.Application.Challenges;
using Diaspora.Modules.Creator.Application.Grants;
using Diaspora.Modules.Creator.Application.Referrals;
using Diaspora.Modules.Creator.Application.Rewards;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Creator.Api;

/// <summary>CREATOR-GAP-004. Challenges.</summary>
internal static class ChallengeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/", async (
                [FromQuery] string? state,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListChallengesQuery { State = state, Take = take }, cancellationToken)))
            .WithName("ListChallenges")
            .WithSummary(
                "Challenge discovery. The state filter takes a group (active, open, finished) or a "
                + "single state, so a three-tab screen makes one request.");

        group.MapGet("/{challengeId:guid}", async (
                Guid challengeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetChallengeQuery { ChallengeId = challengeId }, cancellationToken)))
            .WithName("GetChallenge")
            .WithSummary("One challenge, with this creator's per-task progress.");

        group.MapGet("/{challengeId:guid}/leaderboard", async (
                Guid challengeId,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetChallengeLeaderboardQuery { ChallengeId = challengeId, Take = take },
                        cancellationToken)))
            .WithName("GetChallengeLeaderboard")
            .WithSummary("The challenge's ranking, with the caller's own row.");

        group.MapPost("/{challengeId:guid}/join", async (
                Guid challengeId,
                ClaimRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new JoinChallengeCommand
                        {
                            ChallengeId = challengeId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("JoinChallenge")
            .WithSummary("Joins a challenge. Idempotent; a second join returns the first enrolment.");

        group.MapPost("/{challengeId:guid}/claim", async (
                Guid challengeId,
                ClaimRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ClaimChallengeCommand
                        {
                            ChallengeId = challengeId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("ClaimChallenge")
            .WithSummary("Claims a completed challenge. Idempotent; a repeat never pays twice.");
    }
}

/// <summary>CREATOR-GAP-005. Community grants.</summary>
internal static class GrantEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/", async (
                [FromQuery] string? category,
                [FromQuery] string? country,
                [FromQuery] string? state,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGrantsQuery
                        {
                            Category = category,
                            CountryCode = country,
                            State = state,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListGrants")
            .WithSummary("Grant discovery. ClosingSoon is the server's decision, never a device's.");

        // Literal before parameterised: /grants/applications/me must not be read as a grant id.
        group.MapGet("/applications/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyGrantApplicationsQuery(), cancellationToken)))
            .WithName("ListMyGrantApplications")
            .WithSummary("The caller's own applications.");

        group.MapGet("/applications/review", async (
                [FromQuery] Guid? grantId,
                [FromQuery] string? state,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGrantApplicationsForReviewQuery
                        {
                            GrantId = grantId,
                            State = state,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListGrantApplicationsForReview")
            .WithSummary("The reviewer's queue. Reviewer only.");

        group.MapPost("/applications/{applicationId:guid}/withdraw", async (
                Guid applicationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawGrantApplicationCommand { ApplicationId = applicationId },
                        cancellationToken)))
            .WithName("WithdrawGrantApplication")
            .WithSummary("Withdraws the caller's own application. The only decision an applicant makes.");

        group.MapPost("/applications/{applicationId:guid}/reports", async (
                Guid applicationId,
                SubmitGrantReportRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitGrantReportCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SubmitGrantReport")
            .WithSummary("Files a report against an active grant.");

        group.MapPost("/applications/{applicationId:guid}/decide", async (
                Guid applicationId,
                DecideGrantApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideGrantApplicationCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideGrantApplication")
            .WithSummary(
                "Reviews, requests more, approves, refuses or completes. Reviewer only, and never "
                + "the reviewer's own application.");

        group.MapGet("/{grantId:guid}", async (
                Guid grantId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetGrantQuery { GrantId = grantId }, cancellationToken)))
            .WithName("GetGrant")
            .WithSummary("One grant, with the caller's eligibility decided by the server.");

        group.MapGet("/{grantId:guid}/form", async (
                Guid grantId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetGrantFormQuery { GrantId = grantId }, cancellationToken)))
            .WithName("GetGrantForm")
            .WithSummary(
                "The served application form. Adding a question is a backend change rather than an "
                + "app release.");

        group.MapPost("/{grantId:guid}/applications", async (
                Guid grantId,
                SaveGrantApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SaveGrantApplicationCommand { GrantId = grantId, Request = request },
                        cancellationToken)))
            .WithName("SaveGrantApplication")
            .WithSummary("Starts or updates a draft. One live application per creator per grant.");

        group.MapPost("/{grantId:guid}/applications/submit", async (
                Guid grantId,
                SaveGrantApplicationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitGrantApplicationCommand
                        {
                            GrantId = grantId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("SubmitGrantApplication")
            .WithSummary("Submits. Idempotent on the key, and validated against the served form.");
    }
}

/// <summary>CREATOR-GAP-006. The Creator Academy.</summary>
internal static class AcademyEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/sections", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new ListAcademySectionsQuery(), cancellationToken)))
            .WithName("ListAcademySections")
            .WithSummary("The Academy's sections, with the courses in each.");

        group.MapGet("/courses", async (
                [FromQuery] string? section,
                [FromQuery] string? level,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListAcademyCoursesQuery { Section = section, Level = level },
                        cancellationToken)))
            .WithName("ListAcademyCourses")
            .WithSummary("The course catalogue, with this member's progress and content version.");

        group.MapGet("/courses/{courseId:guid}/lessons", async (
                Guid courseId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCourseLessonsQuery { CourseId = courseId }, cancellationToken)))
            .WithName("ListCourseLessons")
            .WithSummary(
                "A course's lessons as structured blocks. A quiz is served without its answers.");

        group.MapPut("/courses/{courseId:guid}/saved", async (
                Guid courseId,
                SaveCourseRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SaveCourseCommand { CourseId = courseId, Request = request },
                        cancellationToken)))
            .WithName("SaveAcademyCourse")
            .WithSummary("Saves or unsaves a course.");

        group.MapPost("/lessons/{lessonId:guid}/complete", async (
                Guid lessonId,
                CompleteLessonRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CompleteLessonCommand { LessonId = lessonId, Request = request },
                        cancellationToken)))
            .WithName("CompleteLesson")
            .WithSummary(
                "Marks a lesson read. Refused on a quiz lesson with creator.lesson.quiz_required.");

        group.MapPost("/quizzes/{quizId:guid}/submit", async (
                Guid quizId,
                SubmitQuizRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitQuizCommand { QuizId = quizId, Request = request },
                        cancellationToken)))
            .WithName("SubmitQuiz")
            .WithSummary("Grades a submission on the server. The answer key never leaves it.");

        group.MapGet("/certificates/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new ListMyCertificatesQuery(), cancellationToken)))
            .WithName("ListMyCertificates")
            .WithSummary("Certificates, issued against a persisted completion and nothing else.");
    }
}

/// <summary>CREATOR-GAP-009. Referrals.</summary>
internal static class ReferralEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyReferralProgrammeQuery(), cancellationToken)))
            .WithName("GetMyReferralProgramme")
            .WithSummary(
                "The caller's code, counts and the qualification rule in the platform's own words.");

        group.MapGet("/me/history", async (
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyReferralHistoryQuery { Cursor = cursor, Take = take },
                        cancellationToken)))
            .WithName("GetMyReferralHistory")
            .WithSummary("The caller's referrals, cursor-paged, with each one's own state.");

        group.MapPost("/code", async (
                IssueReferralCodeRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new IssueReferralCodeCommand { Request = request }, cancellationToken)))
            .WithName("IssueReferralCode")
            .WithSummary("Issues a code where none exists. Idempotent on the creator.");
    }
}

/// <summary>CREATOR-GAP-010 and -011. The sign-up bonus, the rules and redemptions.</summary>
internal static class RewardEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/rules", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetRewardRulesQuery(), cancellationToken)))
            .WithName("GetCreatorRewardRules")
            .WithSummary(
                "What each action pays, with its cap, its cooldown, the catalogue version and the "
                + "expiry policy.");

        group.MapGet("/catalogue", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetRewardCatalogueQuery(), cancellationToken)))
            .WithName("GetCreatorRewardCatalogue")
            .WithSummary(
                "What points buy, with each reward's state decided for this caller and the balance "
                + "read from the Financial Platform.");

        group.MapGet("/redemptions/me", async (
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyRedemptionsQuery { Take = take }, cancellationToken)))
            .WithName("ListMyCreatorRedemptions")
            .WithSummary("The caller's redemptions.");

        group.MapPost("/redemptions", async (
                RedeemRewardRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RedeemRewardCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("RedeemCreatorReward")
            .WithSummary(
                "Redeems a reward. The price is the catalogue's; the request carries no points "
                + "figure at all.");

        group.MapGet("/signup-bonus/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMySignupBonusQuery(), cancellationToken)))
            .WithName("GetMySignupBonus")
            .WithSummary("The sign-up bonus as a state. A blocked reason never names a check.");

        group.MapPost("/signup-bonus/acknowledge", async (
                AcknowledgeSignupBonusRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AcknowledgeSignupBonusCommand
                        {
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("AcknowledgeSignupBonus")
            .WithSummary(
                "Acknowledges the bonus. Idempotent, and the server decides what it is worth.");
    }
}
