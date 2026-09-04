using Diaspora.Modules.Financial.Application.Advertising;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Advertising campaigns — MON-GAP-001, -006 and -007.
/// <para>
/// <b><c>/financial/campaigns</c>, and the noun is shared with something else on this platform.</b>
/// <c>/api/v1/communities/campaigns</c> is B15.1's fundraising campaigns, mapped by B18 because it
/// owns that prefix. These are advertising campaigns and they are a different entity: different
/// aggregate, different table, different error prefix. The two URL spaces keep them apart, and
/// anybody reading a log line with "campaigns" in it needs the prefix to know which they have.
/// </para>
/// <para>
/// <b>The advertiser's routes and the reviewer's routes are gated differently.</b> Everything under
/// <c>/campaigns/{id}</c> loads through an ownership check and answers absent for a campaign
/// somebody else owns — a 403 would confirm to a competitor that a guessed id is real. The queue and
/// the decision read across owners and take a staff permission instead, because a queue of
/// everybody's creative, budget and targeting is not something a purchase permission should open.
/// </para>
/// </summary>
internal static class AdCampaignEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapAdvertiserRoutes(group);
        MapReviewRoutes(group);
    }

    private static void MapAdvertiserRoutes(RouteGroupBuilder group)
    {
        group.MapPost("/campaigns", async (
                CreateAdCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateAdCampaignCommand { Request = request }, cancellationToken)))
            .WithName("CreateAdCampaign")
            .WithSummary("Drafts an advertising campaign. Nothing is charged and nothing is booked.");

        // Before /campaigns/{campaignId:guid} in the file so the pair reads together. The :guid
        // constraint is what stops the literal being swallowed.
        group.MapGet("/campaigns/me", async (
                [FromQuery] string? status,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyAdCampaignsQuery { Status = status, Take = take },
                        cancellationToken)))
            .WithName("GetMyAdCampaigns")
            .WithSummary("The caller's campaigns, newest first. Every state unless filtered.");

        group.MapGet("/campaigns/{campaignId:guid}", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetAdCampaignQuery { CampaignId = campaignId }, cancellationToken)))
            .WithName("GetAdCampaign")
            .WithSummary("One campaign, with its authoritative status and what the caller may do.");

        group.MapPut("/campaigns/{campaignId:guid}", async (
                Guid campaignId,
                UpdateAdCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateAdCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("UpdateAdCampaign")
            .WithSummary("Rewrites a draft. Refused once the campaign has been submitted.");

        group.MapPost("/campaigns/{campaignId:guid}/submit", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitAdCampaignCommand { CampaignId = campaignId },
                        cancellationToken)))
            .WithName("SubmitAdCampaign")
            .WithSummary(
                "Submits for review and freezes the campaign. Answers PendingPayment — the client "
                + "funds it through the existing purchase route.");

        // Pause and resume are two routes over one command rather than one route with a body,
        // because the register names two and because "POST /pause" is a thing an operator can
        // reason about in a log line.
        group.MapPost("/campaigns/{campaignId:guid}/pause", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetAdCampaignRunStateCommand { CampaignId = campaignId, Paused = true },
                        cancellationToken)))
            .WithName("PauseAdCampaign")
            .WithSummary("Stops a running campaign. Spend freezes; nothing is settled.");

        group.MapPost("/campaigns/{campaignId:guid}/resume", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetAdCampaignRunStateCommand { CampaignId = campaignId, Paused = false },
                        cancellationToken)))
            .WithName("ResumeAdCampaign")
            .WithSummary(
                "Restarts a paused campaign. One whose schedule ran out while paused completes "
                + "instead of resuming.");

        group.MapPost("/campaigns/{campaignId:guid}/cancel", async (
                Guid campaignId,
                CancelAdCampaignRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelAdCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("CancelAdCampaign")
            .WithSummary("Stops a campaign for good and records the unspent budget as owed.");
    }

    private static void MapReviewRoutes(RouteGroupBuilder group)
    {
        group.MapGet("/campaigns/{campaignId:guid}/review", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetAdCampaignReviewQuery { CampaignId = campaignId },
                        cancellationToken)))
            .WithName("GetAdCampaignReview")
            .WithSummary("The decision, the reviewer's note and whether it can still be appealed.");

        group.MapPost("/campaigns/{campaignId:guid}/appeal", async (
                Guid campaignId,
                AppealAdCampaignRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AppealAdCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("AppealAdCampaign")
            .WithSummary("The advertiser contests a rejection. Once, and the status does not move.");

        // ── Staff ───────────────────────────────────────────────────────────────────────────
        //
        // Under /campaigns/reviews rather than /campaigns/{id}/decide, so the whole reviewer
        // surface is one path prefix somebody can reason about — and so a permission audit reading
        // the route table can see it in one place.

        group.MapGet("/campaigns/reviews/queue", async (
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetAdCampaignReviewQueueQuery { Take = take }, cancellationToken)))
            .WithName("GetAdCampaignReviewQueue")
            .WithSummary("Campaigns waiting to be reviewed, oldest first. Staff only.");

        group.MapPost("/campaigns/reviews/{campaignId:guid}", async (
                Guid campaignId,
                DecideAdCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideAdCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("DecideAdCampaign")
            .WithSummary(
                "Approves or rejects. A rejection needs a reason; nobody may decide their own.");
    }
}
