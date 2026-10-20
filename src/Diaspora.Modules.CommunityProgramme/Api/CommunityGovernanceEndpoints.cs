using Diaspora.Modules.CommunityProgramme.Application.Communities;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// A community's figures, rules, notices and office holders, and a member's community preferences —
/// FRONTEND-PHASE13-GAP-007, -008, -012, -016, -018, -019 and -021, 21 Oct 2026.
/// <para>
/// <b>At the paths the app's development adapter already calls</b>
/// (<c>src/lib/community-gap-directory-api.ts</c>), so moving a screen onto them is an import swap:
/// <c>/compare</c>, <c>/{idOrSlug}/statistics</c>, <c>/{idOrSlug}/rules</c>,
/// <c>/{idOrSlug}/notices</c>, <c>/{idOrSlug}/roles</c> and <c>/preferences/me</c>. The preferences
/// are also answered at <c>/me/preferences</c>, beside the caller's other <c>/me</c> routes.
/// </para>
/// <para>
/// <b>Two new first segments, both reserved.</b> <c>compare</c> was already a reserved handle;
/// <c>preferences</c> is added to <see cref="Domain.Communities.CommunitySlug"/> with these routes, so
/// a community can never be named into their place.
/// </para>
/// <para>
/// <b>The reads are anonymous</b>, exactly as the community itself is: what a public community asks of
/// its members, says to them, and who runs it are things somebody deciding whether to join is
/// entitled to see. Each handler decides per community, and answers a community the caller may not
/// read with the same not-found as the community itself.
/// </para>
/// </summary>
internal static class CommunityGovernanceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapStatistics(group);
        MapRules(group);
        MapNotices(group);
        MapRoleHolders(group);
        MapPreferences(group);
    }

    private static void MapStatistics(RouteGroupBuilder group)
    {
        group.MapGet("/compare", async (
                [FromQuery] string? ids,
                [FromQuery] string? period,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CompareCommunitiesQuery { Ids = ids, Period = period },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("CompareCommunities")
            .WithSummary(
                "Two to four communities' figures side by side, aligned by metric. No winner, ranking "
                + "or score, and no restricted figure.");

        group.MapGet("/{idOrSlug}/statistics", async (
                string idOrSlug,
                [FromQuery] string? period,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityStatisticsQuery { IdOrSlug = idOrSlug, Period = period },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityStatistics")
            .WithSummary(
                "A community's figures for 7d, 30d, 90d, 12m or all: members, joins, notices and rules, "
                + "plus departures and members' activity for those who run it. Counted, never estimated.");
    }

    private static void MapRules(RouteGroupBuilder group)
    {
        group.MapGet("/{idOrSlug}/rules", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityRulesQuery { IdOrSlug = idOrSlug }, cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityRules")
            .WithSummary("A community's rules, in order. As public as the community.");

        group.MapPost("/{idOrSlug}/rules", async (
                string idOrSlug,
                SaveCommunityRuleRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateCommunityRuleCommand { IdOrSlug = idOrSlug, Request = request },
                        cancellationToken)))
            .WithName("CreateCommunityRule")
            .WithSummary("Add a rule. Needs canManageRules: the owner or an administrator.");

        group.MapPut("/{idOrSlug}/rules/{ruleId:guid}", async (
                string idOrSlug,
                Guid ruleId,
                SaveCommunityRuleRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCommunityRuleCommand
                        {
                            IdOrSlug = idOrSlug,
                            RuleId = ruleId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateCommunityRule")
            .WithSummary("Replace a rule's text, and optionally move it. Needs canManageRules.");

        group.MapDelete("/{idOrSlug}/rules/{ruleId:guid}", async (
                string idOrSlug,
                Guid ruleId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteCommunityRuleCommand { IdOrSlug = idOrSlug, RuleId = ruleId },
                        cancellationToken)))
            .WithName("DeleteCommunityRule")
            .WithSummary("Remove a rule. Answers the rules that remain, renumbered. Needs canManageRules.");
    }

    private static void MapNotices(RouteGroupBuilder group)
    {
        group.MapGet("/{idOrSlug}/notices", async (
                string idOrSlug,
                [FromQuery] bool? includeExpired,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityNoticesQuery
                        {
                            IdOrSlug = idOrSlug,
                            IncludeExpired = includeExpired ?? false
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityNotices")
            .WithSummary(
                "A community's notices, pinned first then newest. Open communities to anybody; private "
                + "ones to their current members.");

        group.MapPost("/{idOrSlug}/notices", async (
                string idOrSlug,
                SaveCommunityNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishCommunityNoticeCommand { IdOrSlug = idOrSlug, Request = request },
                        cancellationToken)))
            .WithName("PublishCommunityNotice")
            .WithSummary("Publish a notice to the community. Needs canPublishNotice.");

        group.MapPut("/{idOrSlug}/notices/{noticeId:guid}", async (
                string idOrSlug,
                Guid noticeId,
                SaveCommunityNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCommunityNoticeCommand
                        {
                            IdOrSlug = idOrSlug,
                            NoticeId = noticeId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateCommunityNotice")
            .WithSummary("Revise a notice. Its author while they belong, or anybody with canPublishNotice.");

        group.MapDelete("/{idOrSlug}/notices/{noticeId:guid}", async (
                string idOrSlug,
                Guid noticeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteCommunityNoticeCommand { IdOrSlug = idOrSlug, NoticeId = noticeId },
                        cancellationToken)))
            .WithName("DeleteCommunityNotice")
            .WithSummary("Remove a notice. Its author while they belong, or anybody with canPublishNotice.");
    }

    private static void MapRoleHolders(RouteGroupBuilder group)
    {
        group.MapGet("/{idOrSlug}/roles", async (
                string idOrSlug,
                [FromQuery] string? role,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityRoleHoldersQuery { IdOrSlug = idOrSlug, Role = role },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityRoleHolders")
            .WithSummary(
                "Who holds an office — owner, administrators, moderators, ambassadors — as each chose to "
                + "be seen: Public to anybody, CommunityOnly to members. ?role= narrows it.");
    }

    private static void MapPreferences(RouteGroupBuilder group)
    {
        // Two paths, one pair of handlers: the adapter's and the one beside /me/memberships.
        foreach (var (path, suffix) in new[] { ("/preferences/me", string.Empty), ("/me/preferences", "AtMe") })
        {
            group.MapGet(path, async (
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(new GetMyCommunityPreferencesQuery(), cancellationToken)))
                .WithName("GetMyCommunityPreferences" + suffix)
                .WithSummary(
                    "The caller's community preferences, or the privacy-safe defaults with isSaved false.");

            group.MapPut(path, async (
                    SaveCommunityPreferencesRequest request,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(
                            new SaveMyCommunityPreferencesCommand { Request = request },
                            cancellationToken)))
                .WithName("SaveMyCommunityPreferences" + suffix)
                .WithSummary(
                    "Save the caller's community preferences. Omitted fields keep their stored value.");
        }
    }
}
