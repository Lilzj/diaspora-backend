using Diaspora.Modules.Financial.Application.Campaigns;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Platform.Http;
using Diaspora.SharedKernel.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// COMMUNITY-LIFE-GAP-006 and -007: the fundraising campaign surface, at the register's paths.
/// <para>
/// <b>Not one decision on this page is made here.</b> Every route dispatches a command or query that
/// lives in B15, which owns the campaign aggregate, its twelve-state machine, the donation cause a
/// publication creates and every figure any of these responses carry. This file maps URLs.
/// </para>
/// <para>
/// <b>Why the routes are here at all.</b> The register puts them under <c>/api/v1/communities</c>
/// and this platform allows exactly one module per URL prefix — a rule an architecture test enforces,
/// because two modules claiming one prefix is an ambiguous route ASP.NET Core reports at request time,
/// in production. So the module that owns the prefix maps them and forwards, which is the same split
/// this module already uses to expose a Community Life write: <b>the route is ours and the decision is
/// theirs.</b> Ownership follows the aggregate, never the URL.
/// </para>
/// <para>
/// <b>Literal segments before parameterised ones</b>, as everywhere else in this module: the six
/// lifecycle verbs are mapped as literals under <c>/{campaignId:guid}/</c>, and the <c>:guid</c>
/// constraint is what stops <c>/campaigns/{campaignId}</c> swallowing anything.
/// </para>
/// </summary>
internal static class CampaignEndpoints
{
    /// <summary>
    /// The six lifecycle verbs, and the state each moves to.
    /// <para>
    /// A table rather than six handlers: submit, publish, pause, resume, close and cancel are one
    /// operation with a destination, and six copies would be six places the transition table is
    /// consulted — five of which would eventually stop being consulted the same way. The destination
    /// comes from the route, never from the body, so a client cannot ask for a state the route it
    /// called does not mean.
    /// </para>
    /// </summary>
    private static readonly (string Verb, string Target)[] Lifecycle =
    [
        ("submit", "PendingReview"),
        ("publish", "Published"),
        ("pause", "Paused"),
        ("resume", "Active"),
        ("close", "Closed"),
        ("cancel", "Cancelled")
    ];

    /// <summary>Pages of a hundred read for <c>/campaigns/me</c> before it stops.</summary>
    private const int MyCampaignPages = 5;

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapCrud(group);
        MapLifecycle(group);
        MapTransparency(group);
    }

    private static void MapCrud(RouteGroupBuilder group)
    {
        group.MapPost("/campaigns", async (
                CreateCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateCampaignCommand { Request = request }, cancellationToken)))
            .WithName("CreateCampaign")
            .WithSummary(
                "Creates a fundraising campaign, in draft. Terms acceptance is refused here, not "
                + "only in the form. Idempotent.");

        group.MapGet("/campaigns", async (
                [FromQuery] string? state,
                [FromQuery] string? category,
                [FromQuery] string? country,
                [FromQuery] Guid? organizationId,
                [FromQuery] bool? mine,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchCampaignsQuery
                        {
                            State = state,
                            Category = category,
                            CountryCode = country,
                            OrganizationId = organizationId,
                            MineOnly = mine ?? false,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("SearchCampaigns")
            .WithSummary(
                "Campaigns a member can browse. A draft is visible only to the person who wrote it.");

        // 21 Oct 2026. The campaigns the caller organises, drafts included, as one list — the path
        // the app's development adapter called. It is the search above with mine=true, read to the
        // end: no second query, no second rule about whose campaign is whose. `me` cannot collide
        // with /campaigns/{campaignId:guid}, which only matches a Guid.
        group.MapGet("/campaigns/me", async (
                [FromQuery] Guid? organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var campaigns = new List<CampaignResponse>();
                string? cursor = null;

                // Bounded: an organiser with more than this is paging, and should use the search.
                for (var page = 0; page < MyCampaignPages; page++)
                {
                    var result = await sender.Send(
                        new SearchCampaignsQuery
                        {
                            MineOnly = true,
                            OrganizationId = organizationId,
                            Cursor = cursor,
                            Take = 100
                        },
                        cancellationToken);

                    if (result.IsFailure)
                    {
                        return PlatformResults.Ok(context, result);
                    }

                    campaigns.AddRange(result.Value.Items);

                    if (!result.Value.HasMore || string.IsNullOrEmpty(result.Value.NextCursor))
                    {
                        break;
                    }

                    cursor = result.Value.NextCursor;
                }

                return PlatformResults.Ok(
                    context, Result.Success<IReadOnlyList<CampaignResponse>>(campaigns));
            })
            .WithName("ListMyCampaigns")
            .WithSummary(
                "The campaigns the caller organises, drafts included, as one list (at most 500). "
                + "The same rule as /campaigns?mine=true.");

        group.MapGet("/campaigns/{campaignId:guid}", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCampaignQuery { CampaignId = campaignId }, cancellationToken)))
            .WithName("GetCampaign")
            .WithSummary("One campaign, with server-computed progress and canDonate.");

        group.MapPut("/campaigns/{campaignId:guid}", async (
                Guid campaignId,
                UpdateCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("UpdateCampaign")
            .WithSummary("Edits a campaign's own text. Nothing financial moves through it.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        foreach (var (verb, target) in Lifecycle)
        {
            // Captured per iteration. A closure over the loop variable would give every route the
            // last destination in the table, and all six would publish.
            var destination = target;

            group.MapPost($"/campaigns/{{campaignId:guid}}/{verb}", async (
                    Guid campaignId,
                    CampaignTransitionRequest? request,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(
                            new TransitionCampaignCommand
                            {
                                CampaignId = campaignId,
                                Target = destination,
                                Request = request
                            },
                            cancellationToken)))
                .WithName($"Campaign{char.ToUpperInvariant(verb[0])}{verb[1..]}")
                .WithSummary(
                    $"Moves a campaign to {destination}. Answers 409 when the state machine "
                    + "forbids it.");
        }
    }

    private static void MapTransparency(RouteGroupBuilder group)
    {
        group.MapGet("/campaigns/{campaignId:guid}/updates", async (
                Guid campaignId,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCampaignUpdatesQuery
                        {
                            CampaignId = campaignId,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("GetCampaignUpdates")
            .WithSummary("A campaign's timeline, newest first. Cursor-paged.");

        group.MapPost("/campaigns/{campaignId:guid}/updates", async (
                Guid campaignId,
                CreateCampaignUpdateRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishCampaignUpdateCommand
                        {
                            CampaignId = campaignId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("PublishCampaignUpdate")
            .WithSummary("Publishes an update to a campaign's supporters. Idempotent.");

        group.MapGet("/campaigns/{campaignId:guid}/milestones", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCampaignMilestonesQuery { CampaignId = campaignId },
                        cancellationToken)))
            .WithName("GetCampaignMilestones")
            .WithSummary(
                "A campaign's milestones. Whether one is reached is the server's own stamp, taken "
                + "from the cause's authoritative total.");

        group.MapPut("/campaigns/{campaignId:guid}/milestones", async (
                Guid campaignId,
                SetCampaignMilestonesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetCampaignMilestonesCommand
                        {
                            CampaignId = campaignId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SetCampaignMilestones")
            .WithSummary("Declares a campaign's milestones. Reached-ness is never the caller's.");

        group.MapGet("/campaigns/{campaignId:guid}/supporters", async (
                Guid campaignId,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCampaignSupportersQuery
                        {
                            CampaignId = campaignId,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("GetCampaignSupporters")
            .WithSummary(
                "A campaign's supporters, anonymised at the source. An anonymous supporter carries "
                + "no identifier at all.");

        // The caller's own donations to this campaign. `/donations/me` is a literal under the
        // constrained id, so it cannot collide with any other campaign route; the donor is the
        // caller, so the path carries no user.
        group.MapGet("/campaigns/{campaignId:guid}/donations/me", async (
                Guid campaignId,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyCampaignDonationsQuery { CampaignId = campaignId, Take = take },
                        cancellationToken)))
            .WithName("GetMyCampaignDonations")
            .WithSummary(
                "The caller's own donations to one campaign, newest first. Anonymous donations are "
                + "absent: they store no donor.");

        group.MapGet("/campaigns/{campaignId:guid}/fund-usage", async (
                Guid campaignId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCampaignFundUsageQuery { CampaignId = campaignId },
                        cancellationToken)))
            .WithName("GetCampaignFundUsage")
            .WithSummary(
                "Raised, allocated, disbursed and remaining — each labelled with the authority "
                + "behind it.");

        group.MapPut("/campaigns/{campaignId:guid}/fund-usage", async (
                Guid campaignId,
                SetCampaignFundUsageRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetCampaignFundUsageCommand
                        {
                            CampaignId = campaignId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SetCampaignFundUsage")
            .WithSummary(
                "Declares how a campaign's money is allocated. A spend is marked verified only "
                + "where a settled payout backs it.");
    }
}
