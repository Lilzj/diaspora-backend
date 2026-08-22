using Diaspora.Modules.Guides.Application.Analytics;
using Diaspora.Modules.Guides.Application.Authoring;
using Diaspora.Modules.Guides.Application.Discovery;
using Diaspora.Modules.Guides.Application.Lifecycle;
using Diaspora.Modules.Guides.Application.Localization;
using Diaspora.Modules.Guides.Application.Media;
using Diaspora.Modules.Guides.Application.Publishing;
using Diaspora.Modules.Guides.Application.Referencing;
using Diaspora.Modules.Guides.Application.Tags;
using Diaspora.Modules.Guides.Application.Verification;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Reading one guide, writing one guide, and moving it through its life.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// telemetry, transactions, authorisation and audit — and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are byte-for-byte identical to
/// Identity's controllers and to Trust's, Location's, Media's, Community's and Providers'
/// endpoints.
/// </para>
/// <para>
/// <b>The two detail reads carry no authorisation, and that is the module's whole point.</b>
/// Published knowledge is public: a member looking for how to register a residence permit must
/// be able to read the answer before anybody asks them to sign up. Visibility is still applied,
/// inside the handler — a draft, a hidden guide and a deleted one resolve as not found to
/// everybody but their author and staff, which is a decision that belongs next to the data
/// rather than in a route table.
/// </para>
/// <para>
/// The editorial sub-resources — tags, category, language, media, provider references — are
/// their own routes rather than fields on the document save, because they are what a curator
/// changes on somebody else's guide, and a whole-document PUT would make that act carry the
/// risk of overwriting the author's in-flight edit.
/// </para>
/// </summary>
internal static class GuideEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapAuthoring(group);
        MapEditorial(group);
        MapPublishing(group);
        MapLifecycle(group);
        MapAuthorTools(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/{guideId:guid}", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetGuideQuery { GuideId = guideId }, cancellationToken)))
            .WithName("GetGuide")
            .WithSummary("One guide in full, by identifier.");

        group.MapGet("/slug/{slug}", async (
                string slug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetGuideBySlugQuery { Slug = slug }, cancellationToken)))
            .WithName("GetGuideBySlug")
            .WithSummary("One guide, by the handle a deep link carries.");

        group.MapGet("/{guideId:guid}/preview", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PreviewGuideQuery { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.ViewDrafts)
            .WithName("PreviewGuide")
            .WithSummary("The draft rendered exactly as the detail screen would render it published.");

        group.MapGet("/mine", async (
                string? status,
                string? sort,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyGuidesQuery
                        {
                            Status = status,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.ViewDrafts)
            .WithName("GetMyGuides")
            .WithSummary("The author's own workspace — every guide they wrote, in whatever state.");

        group.MapGet("/mine/drafts", async (
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyDraftsQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.ViewDrafts)
            .WithName("GetMyDrafts")
            .WithSummary("What the author has not finished writing.");
    }

    private static void MapAuthoring(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                CreateGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateGuideCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.Create)
            .WithName("CreateGuide")
            .WithSummary("Open a new guide. It is created as a draft.");

        // BG-07. The quick-actions sheet's "Duplicate" was inert across several phases because no
        // module had a clone route. Returns a fresh draft authored by the caller; see
        // DuplicateGuideCommandHandler for exactly what is and is not carried over.
        group.MapPost("/{guideId:guid}/duplicate", async (
                Guid guideId,
                DuplicateGuideRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new DuplicateGuideCommand { GuideId = guideId, Title = request?.Title },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Create)
            .WithName("DuplicateGuide")
            .WithSummary("Copy a guide you wrote into a new draft.");

        group.MapPut("/{guideId:guid}", async (
                Guid guideId,
                UpdateGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateGuideCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("UpdateGuide")
            .WithSummary("The editor's save: metadata, and the whole structure when one is sent.");

        group.MapPut("/{guideId:guid}/draft", async (
                Guid guideId,
                SaveGuideDraftRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SaveGuideDraftCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("SaveGuideDraft")
            .WithSummary("The editor's auto-save. Kept aside; it never touches the guide.");

        group.MapPost("/{guideId:guid}/translations", async (
                Guid guideId,
                CreateGuideTranslationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateGuideTranslationCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Create)
            .WithName("CreateGuideTranslation")
            .WithSummary("Start a translation. It is a separate guide sharing a translation group.");
    }

    private static void MapEditorial(RouteGroupBuilder group)
    {
        group.MapPut("/{guideId:guid}/tags", async (
                Guid guideId,
                SetGuideTagsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideTagsCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.ManageTags)
            .WithName("SetGuideTags")
            .WithSummary("Replace the guide's tag set wholesale, as the chip editor saves it.");

        group.MapPut("/{guideId:guid}/category", async (
                Guid guideId,
                SetGuideCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideCategoryCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("SetGuideCategory")
            .WithSummary("Refile the guide under another catalogue category.");

        group.MapPut("/{guideId:guid}/language", async (
                Guid guideId,
                SetGuideLanguageRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideLanguageCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("SetGuideLanguage")
            .WithSummary("Correct the language the guide claims to be written in.");

        group.MapPost("/{guideId:guid}/media", async (
                Guid guideId,
                AttachGuideMediaRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AttachGuideMediaCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("AttachGuideMedia")
            .WithSummary("Attach an uploaded asset by reference. Idempotent on media and role.");

        group.MapDelete("/{guideId:guid}/media/{mediaId:guid}/{role}", async (
                Guid guideId,
                Guid mediaId,
                string role,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DetachGuideMediaCommand
                        {
                            GuideId = guideId,
                            MediaId = mediaId,
                            Role = role
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("DetachGuideMedia")
            .WithSummary("Drop a media reference. The asset itself is untouched. Idempotent.");

        group.MapPut("/{guideId:guid}/providers", async (
                Guid guideId,
                SetGuideProviderReferencesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideProviderReferencesCommand
                        {
                            GuideId = guideId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("SetGuideProviderReferences")
            .WithSummary("Replace the providers the guide points readers at.");
    }

    private static void MapPublishing(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/publish", async (
                Guid guideId,
                PublishGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishGuideCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Publish)
            .WithName("PublishGuide")
            .WithSummary("Publish the current draft as the next version.");

        group.MapPost("/{guideId:guid}/schedule", async (
                Guid guideId,
                ScheduleGuidePublicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SchedulePublicationCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Schedule)
            .WithName("ScheduleGuidePublication")
            .WithSummary("Set the draft to publish itself at a future instant.");

        group.MapDelete("/{guideId:guid}/schedule", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelScheduledPublicationCommand { GuideId = guideId },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Schedule)
            .WithName("CancelScheduledGuidePublication")
            .WithSummary("Call a scheduled publication off before it fires.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/archive", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.ArchiveOwn)
            .WithName("ArchiveGuide")
            .WithSummary("Retire advice that has gone stale. Not a delete — the versions stay.");

        group.MapPost("/{guideId:guid}/restore", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.RestoreOwn)
            .WithName("RestoreGuide")
            .WithSummary("Return an archived guide to publication, exactly as it was.");

        group.MapDelete("/{guideId:guid}", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.DeleteOwn)
            .WithName("DeleteGuide")
            .WithSummary("Remove the guide. Soft, and recoverable.");

        group.MapPost("/{guideId:guid}/undelete", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UndeleteGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.RestoreOwn)
            .WithName("UndeleteGuide")
            .WithSummary("Put a deleted guide back, in whatever status it held.");

        group.MapPut("/{guideId:guid}/featured", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new FeatureGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.Feature)
            .WithName("FeatureGuide")
            .WithSummary("Give a published guide editorial placement. A staff act.");

        group.MapDelete("/{guideId:guid}/featured", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UnfeatureGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.Feature)
            .WithName("UnfeatureGuide")
            .WithSummary("Withdraw editorial placement. Idempotent.");
    }

    private static void MapAuthorTools(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/verification", async (
                Guid guideId,
                SubmitGuideVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitGuideVerificationCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.SubmitVerification)
            .WithName("SubmitGuideVerification")
            .WithSummary("Raise a verification claim. It asks; it does not grant.");

        group.MapGet("/{guideId:guid}/verification", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetGuideVerificationStatusQuery { GuideId = guideId },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.SubmitVerification)
            .WithName("GetGuideVerificationStatus")
            .WithSummary("Where verification stands, with the reason — for the author, not readers.");

        group.MapGet("/{guideId:guid}/analytics", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetGuideAnalyticsQuery { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.ViewAnalytics)
            .WithName("GetGuideAnalytics")
            .WithSummary("The guide's accumulated readership, for the person who wrote it.");
    }
}
