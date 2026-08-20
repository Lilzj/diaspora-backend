using Diaspora.Modules.Guides.Application.Engagement;
using Diaspora.Modules.Guides.Application.Suggestions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// What a reader does with a guide after reading it.
/// <para>
/// The bookmark routes are a <c>PUT</c> and a <c>DELETE</c> on one resource rather than two verbs
/// on a collection, so both are idempotent: a client that retries a save after a timeout gets
/// the same answer, and a double-tapped bookmark cannot produce two. The storage is Community's,
/// because the member's saved-collections screen shows guides beside providers and posts from
/// one store with one folder model.
/// </para>
/// <para>
/// <b>Views and shares carry no authorisation.</b> Anonymous readers are most of the traffic on
/// a knowledge base, and a counter that only counted signed-in members would report the wrong
/// number confidently — which is worse than reporting none. Uniqueness is the client's claim
/// rather than a server-side fingerprint: honest-enough analytics at the price the platform is
/// willing to pay for not tracking people.
/// </para>
/// <para>
/// Suggestions are the platform's lightweight merge request. Listing them is public — seeing
/// that somebody already reported the fee as out of date saves the next reader the trouble and
/// the author the tenth identical correction — while deciding one is the guide owner's, and
/// withdrawing one is the suggester's alone.
/// </para>
/// </summary>
internal static class GuideEngagementEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapBookmarks(group);
        MapReactions(group);
        MapSuggestions(group);
    }

    private static void MapBookmarks(RouteGroupBuilder group)
    {
        group.MapGet("/bookmarked", async (
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListBookmarkedGuidesQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Bookmark)
            .WithName("ListBookmarkedGuides")
            .WithSummary("The guides the member saved. Personal, and nobody else's business.");

        group.MapPut("/{guideId:guid}/bookmark", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideBookmarkCommand { GuideId = guideId, IsSaved = true },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Bookmark)
            .WithName("SaveGuide")
            .WithSummary("Save a guide. Idempotent.");

        group.MapDelete("/{guideId:guid}/bookmark", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetGuideBookmarkCommand { GuideId = guideId, IsSaved = false },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Bookmark)
            .WithName("UnsaveGuide")
            .WithSummary("Remove a saved guide. Idempotent.");
    }

    private static void MapReactions(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/vote", async (
                Guid guideId,
                CastGuideVoteRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CastGuideVoteCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Vote)
            .WithName("CastGuideVote")
            .WithSummary("Did this guide help? One vote per member; casting again changes it.");

        group.MapPost("/{guideId:guid}/view", async (
                Guid guideId,
                RecordGuideViewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordGuideViewCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .WithName("RecordGuideView")
            .WithSummary("A reader opened the guide, and how thoroughly they read it.");

        group.MapPost("/{guideId:guid}/share", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ShareGuideCommand { GuideId = guideId }, cancellationToken)))
            .WithName("ShareGuide")
            .WithSummary("A reader shared it. The client reports what the platform cannot see.");

        group.MapPost("/{guideId:guid}/report", async (
                Guid guideId,
                ReportGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ReportGuideCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Report)
            .WithName("ReportGuide")
            .WithSummary("Report a guide. The workflow that follows is Community's.");
    }

    private static void MapSuggestions(RouteGroupBuilder group)
    {
        group.MapGet("/{guideId:guid}/suggestions", async (
                Guid guideId,
                string? status,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuideSuggestionsQuery
                        {
                            GuideId = guideId,
                            Status = status,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListGuideSuggestions")
            .WithSummary("The improvement suggestions on one guide.");

        group.MapPost("/{guideId:guid}/suggestions", async (
                Guid guideId,
                SubmitGuideSuggestionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SubmitGuideSuggestionCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Suggest)
            .WithName("SubmitGuideSuggestion")
            .WithSummary("Propose an improvement. Accepting one does not edit the guide.");

        group.MapPost("/suggestions/{suggestionId:guid}/decision", async (
                Guid suggestionId,
                DecideGuideSuggestionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideGuideSuggestionCommand
                        {
                            SuggestionId = suggestionId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("DecideGuideSuggestion")
            .WithSummary("Accept or decline. The note reaches the suggester either way.");

        group.MapPost("/suggestions/{suggestionId:guid}/withdrawal", async (
                Guid suggestionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawGuideSuggestionCommand { SuggestionId = suggestionId },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Suggest)
            .WithName("WithdrawGuideSuggestion")
            .WithSummary("The suggester takes their proposal back. Only they can.");
    }
}
