using Diaspora.Modules.Financial.Application.Campaigns;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// The staff side of fundraising campaigns: suspending a live one and lifting a suspension.
/// <para>
/// <b>Here, and not beside the organiser's routes under <c>/communities/campaigns</c>.</b> Those are
/// the register's paths, mapped by the module that owns that prefix, and every one of them is the
/// organiser's. A staff action belongs on a staff route the permission audit can find in one place,
/// and <c>/financial/campaigns</c> is advertising — so fundraising moderation sits under
/// <c>/financial/giving/campaigns</c>.
/// </para>
/// </summary>
internal static class CampaignModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/giving/campaigns/{campaignId:guid}/moderation", async (
                Guid campaignId,
                ModerateCampaignRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ModerateCampaignCommand { CampaignId = campaignId, Request = request },
                        cancellationToken)))
            .WithName("ModerateFundraisingCampaign")
            .WithSummary(
                "Suspends a live fundraiser, or lifts a suspension. Staff only; a reason is "
                + "required, and nobody moderates a campaign they run.");
    }
}
