using Diaspora.Modules.Guides.Application.Moderation;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The staff surface: the moderation queue, and the trail that explains a guide's moderation.
/// <para>
/// Every route here is gated at <see cref="GuidePolicies.Moderate"/> — including the queue read,
/// which is the one read in this module that deliberately sees what nobody else does: hidden
/// guides, flagged guides and the content behind them. A moderator who could not see what they
/// are deciding about would be deciding about a title.
/// </para>
/// <para>
/// <b>Staff no longer hide or unhide a guide here (7 Nov 2026).</b> They decide on the guide's
/// Trust moderation case, with a reason code and a note the author reads, a second member of staff
/// for a removal, and an appeal; <c>GuideModerationTarget</c> carries the decision out. The notes
/// stay: they record, and act on nothing. They are append-only, because a trail that can be tidied
/// afterwards records what somebody later wished had been decided.
/// </para>
/// </summary>
internal static class GuideModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapQueue(group);
        MapNotes(group);
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

    private static void MapNotes(RouteGroupBuilder group)
    {
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
