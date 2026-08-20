using Diaspora.Modules.Guides.Application.Intelligence.Discovery;
using Diaspora.Modules.Guides.Application.Intelligence.Notices;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The official notice lifecycle, from a draft to a superseded record.
/// <para>
/// <b>Three policies across eleven routes, and the split is the phase's separation of duty.</b>
/// Writing is <c>manage</c>, confirming the source is <c>verify</c>, and making it visible is
/// <c>publish</c> — because a workflow where the person who transcribes a circular also approves and
/// publishes it has no verification step at all.
/// </para>
/// <para>
/// The two reads are anonymous. Reading what a government announced needs no account, and a listing
/// narrows itself by the caller's role rather than by the route: an editor sees drafts on the same
/// URL where everybody else sees published notices.
/// </para>
/// </summary>
internal static class OfficialNoticeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapAuthoring(group);
        MapLifecycle(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/notices", async (
                string? countryCode,
                string? category,
                string? severity,
                string? minimumSeverity,
                Guid? authorityId,
                string? language,
                string? q,
                bool? officialOnly,
                bool? activeOnly,
                bool? expiredOnly,
                string? status,
                bool? includeArchived,
                string? sort,
                string? cursor,
                int? limit,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListOfficialNoticesQuery
                        {
                            CountryCode = countryCode,
                            CategoryKey = category,
                            Severities = severity is null ? [] : [severity],
                            MinimumSeverity = minimumSeverity,
                            AuthorityId = authorityId,
                            Language = language,
                            Text = q,
                            OfficialOnly = officialOnly ?? false,
                            ActiveOnly = activeOnly ?? false,
                            ExpiredOnly = expiredOnly ?? false,
                            Statuses = status is null ? [] : [status],
                            IncludeArchived = includeArchived ?? false,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("ListOfficialNotices")
            .WithSummary(
                "Official notices, in briefing order by default. Editors see drafts here too.");

        group.MapGet("/notices/{noticeId:guid}", async (
                Guid noticeId,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOfficialNoticeQuery
                        {
                            OfficialNoticeId = noticeId,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("GetOfficialNotice")
            .WithSummary("One official notice in full, localized where a translation exists.");

        // Anonymous, because reading is. A view that required an account would count only the
        // members, which is a fraction of who reads an emergency advisory.
        group.MapPost("/notices/{noticeId:guid}/activity", async (
                Guid noticeId,
                RecordNoticeActivityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordNoticeActivityCommand
                        {
                            OfficialNoticeId = noticeId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("RecordNoticeActivity")
            .WithSummary(
                "Record a view, a click-through, a read completion or a crossing into a guide.");
    }

    private static void MapAuthoring(RouteGroupBuilder group)
    {
        group.MapPost("/notices", async (
                CreateOfficialNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateOfficialNoticeCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("CreateOfficialNotice")
            .WithSummary("Open a draft. The authority must issue for the notice's country.");

        group.MapPut("/notices/{noticeId:guid}", async (
                Guid noticeId,
                UpdateOfficialNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateOfficialNoticeCommand
                        {
                            OfficialNoticeId = noticeId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("UpdateOfficialNotice")
            .WithSummary(
                "Rewrite a notice. Editing verified text returns it to draft; published text is " +
                "frozen and a correction is a supersede.");

        group.MapPost("/notices/{noticeId:guid}/review", async (
                Guid noticeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReviewOfficialNoticeCommand { OfficialNoticeId = noticeId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("ReviewOfficialNotice")
            .WithSummary("Confirm a notice is still true, without changing a word of it.");

        group.MapDelete("/notices/{noticeId:guid}", async (
                Guid noticeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveOfficialNoticeCommand { OfficialNoticeId = noticeId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("ArchiveOfficialNotice")
            .WithSummary("Withdraw a notice. A soft delete; restorable.");

        group.MapPost("/notices/{noticeId:guid}/restore", async (
                Guid noticeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreOfficialNoticeCommand { OfficialNoticeId = noticeId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("RestoreOfficialNotice")
            .WithSummary("Return an archived notice to the desk. It comes back as a draft.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/notices/{noticeId:guid}/submit", async (
                Guid noticeId,
                int? expectedRevision,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitOfficialNoticeCommand
                        {
                            OfficialNoticeId = noticeId,
                            ExpectedRevision = expectedRevision
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageNotices)
            .WithName("SubmitOfficialNotice")
            .WithSummary("Send a completed draft for verification.");

        group.MapPost("/notices/{noticeId:guid}/verify", async (
                Guid noticeId,
                VerifyOfficialNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new VerifyOfficialNoticeCommand
                        {
                            OfficialNoticeId = noticeId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.VerifyNotices)
            .WithName("VerifyOfficialNotice")
            .WithSummary(
                "Confirm the authority issued this, or refuse it. A refusal returns it to draft.");

        group.MapPost("/notices/{noticeId:guid}/publish", async (
                Guid noticeId,
                int? expectedRevision,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishOfficialNoticeCommand
                        {
                            OfficialNoticeId = noticeId,
                            ExpectedRevision = expectedRevision
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.PublishNotices)
            .WithName("PublishOfficialNotice")
            .WithSummary(
                "Make a verified notice visible. Refused unless the authority is verified and " +
                "every source validator accepts the sourcing.");

        group.MapPost("/notices/{noticeId:guid}/supersede", async (
                Guid noticeId,
                SupersedeOfficialNoticeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SupersedeOfficialNoticeCommand
                        {
                            OfficialNoticeId = noticeId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.PublishNotices)
            .WithName("SupersedeOfficialNotice")
            .WithSummary(
                "Replace a live notice with a later one. The replacement must be published and " +
                "about the same country.");
    }
}
