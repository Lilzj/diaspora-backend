using Diaspora.Modules.Jobs.Application.Interviews;
using Diaspora.Modules.Jobs.Application.Offers;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// Interviews and offers.
/// <para>
/// Note where each route hangs. Scheduling and recording sit under the application, because they
/// are things done <em>to</em> an application. Responding to an offer sits under the offer,
/// because it is the candidate acting on a specific set of terms — and the two have opposite
/// permissions.
/// </para>
/// </summary>
internal static class HiringEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var applications = group.MapGroup("/applications");

        applications.MapPost("/{applicationId:guid}/interviews", async (
                Guid applicationId,
                [FromBody] ScheduleInterviewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ScheduleInterviewCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.InterviewsSchedule)
            .WithName("ScheduleInterview")
            .WithSummary("Schedule an interview round against an application.");

        applications.MapPost("/{applicationId:guid}/offers", async (
                Guid applicationId,
                [FromBody] ExtendOfferRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ExtendOfferCommand
                        {
                            ApplicationId = applicationId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.OffersExtend)
            .WithName("ExtendOffer")
            .WithSummary("Draft and extend an offer.");

        // ── The two reads, added to close gaps G-3 and G-4 ───────────────────────────────────
        //
        // Both writes above have existed since the hiring wave and neither had a corresponding
        // read: `GET` on these exact paths answered **405**, and `/jobs/interviews` and
        // `/jobs/offers` answered **404**. The employer dashboard reported
        // `upcomingInterviewCount` and `outstandingOfferCount` with nothing to open, and a
        // candidate could accept an offer whose terms they had no route to read.
        //
        // The policy is `ApplicationsReview` rather than the scheduling and extending policies
        // above, because reading is what both parties do and only the employer writes. Which of
        // the two is asking — and therefore what they are shown — is settled in the handler.
        applications.MapGet("/{applicationId:guid}/interviews", async (
                Guid applicationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListApplicationInterviewsQuery { ApplicationId = applicationId },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationsReview)
            .WithName("ListApplicationInterviews")
            .WithSummary("The interview rounds on an application, for the employer or the candidate.");

        applications.MapGet("/{applicationId:guid}/offers", async (
                Guid applicationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListApplicationOffersQuery { ApplicationId = applicationId },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.ApplicationsReview)
            .WithName("ListApplicationOffers")
            .WithSummary("The offers extended on an application, most recent first.");

        var interviews = group.MapGroup("/interviews");

        interviews.MapPost("/{interviewId:guid}/outcome", async (
                Guid interviewId,
                [FromBody] RecordInterviewOutcomeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordInterviewOutcomeCommand
                        {
                            InterviewId = interviewId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.InterviewsRecord)
            .WithName("RecordInterviewOutcome")
            .WithSummary("Record what an interview concluded.");

        var offers = group.MapGroup("/offers");

        // The candidate's route, with the candidate's permission — granted under restriction,
        // because an offer is time-limited and must not lapse because somebody was suspended.
        offers.MapPost("/{offerId:guid}/respond", async (
                Guid offerId,
                [FromBody] RespondToOfferRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RespondToOfferCommand { OfferId = offerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(JobPolicies.OffersRespond)
            .WithName("RespondToOffer")
            .WithSummary("Accept or decline an offer made to you.");
    }
}
