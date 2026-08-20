using Diaspora.Modules.Guides.Application.Moderation;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The staff surface: taking a guide off the public shelves, putting it back, and the trail that
/// explains why.
/// <para>
/// Every route here is gated at <see cref="GuidePolicies.Moderate"/> — including the queue read,
/// which is the one read in this module that deliberately sees what nobody else does: hidden
/// guides, flagged guides and the content behind them. A moderator who could not see what they
/// are deciding about would be deciding about a title.
/// </para>
/// <para>
/// Hiding takes a reason and lifting one does not, and the asymmetry is intentional: friction
/// belongs on the restricting side of a moderation decision, never on the side that undoes it.
/// The notes are append-only, because a trail that can be tidied afterwards records what
/// somebody later wished had been decided.
/// </para>
/// </summary>
internal static class GuideModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapQueue(group);
        MapDecisions(group);
    }

    private static void MapQueue(RouteGroupBuilder group)
    {
        group.MapGet("/moderation/queue", async (
                string? moderationState,
                string? countryCode,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListModerationQueueQuery
                        {
                            ModerationState = moderationState,
                            CountryCode = countryCode,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Moderate)
            .WithName("ListGuideModerationQueue")
            .WithSummary("The guides waiting for a moderator — flagged, hidden, or both.");
    }

    private static void MapDecisions(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/hide", async (
                Guid guideId,
                HideGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new HideGuideCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Moderate)
            .WithName("HideGuide")
            .WithSummary("Take the guide off every public surface, reversibly. The reason is required.");

        group.MapPost("/{guideId:guid}/unhide", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UnhideGuideCommand { GuideId = guideId }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.Moderate)
            .WithName("UnhideGuide")
            .WithSummary("Lift the hide, restoring exactly what was there.");

        group.MapPost("/{guideId:guid}/moderation-notes", async (
                Guid guideId,
                AddGuideModerationNoteRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AddGuideModerationNoteCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.Moderate)
            .WithName("AddGuideModerationNote")
            .WithSummary("Append to the editorial audit trail. Never shown to readers.");
    }
}
