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
/// A community's initiatives — FRONTEND-PHASE13-GAP-019, 21 Oct 2026.
/// <para>
/// <b>At the path the app's development adapter already calls</b>
/// (<c>/communities/{id}/initiatives</c> in <c>src/lib/community-gap-directory-api.ts</c>), with the
/// detail, participation and updates beneath it. No new first segment, so no reserved handle.
/// </para>
/// <para>
/// <b>The reads are anonymous</b>, as the community's notices are, and answer a community the caller
/// may not read with the same not-found as the community itself. See
/// <see cref="CommunityInitiativeAccess"/> and the header of <c>CommunityInitiatives.cs</c> for who
/// may write.
/// </para>
/// </summary>
internal static class CommunityInitiativeEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{idOrSlug}/initiatives", async (
                string idOrSlug,
                [FromQuery] string? status,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityInitiativesQuery { IdOrSlug = idOrSlug, Status = status },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityInitiatives")
            .WithSummary(
                "A community's initiatives, newest first, with organisers, participant and update counts. "
                + "?status= narrows it. As public as the community's notices.");

        group.MapPost("/{idOrSlug}/initiatives", async (
                string idOrSlug,
                CreateCommunityInitiativeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateCommunityInitiativeCommand { IdOrSlug = idOrSlug, Request = request },
                        cancellationToken)))
            .WithName("CreateCommunityInitiative")
            .WithSummary(
                "Start an initiative. Needs canManageInitiatives: the owner, an administrator or an "
                + "ambassador. No money: fundraising is a campaign's.");

        group.MapGet("/{idOrSlug}/initiatives/{initiativeId:guid}", async (
                string idOrSlug,
                Guid initiativeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityInitiativeQuery { IdOrSlug = idOrSlug, InitiativeId = initiativeId },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityInitiative")
            .WithSummary("One initiative, with what this caller may do with it.");

        group.MapPut("/{idOrSlug}/initiatives/{initiativeId:guid}", async (
                string idOrSlug,
                Guid initiativeId,
                UpdateCommunityInitiativeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCommunityInitiativeCommand
                        {
                            IdOrSlug = idOrSlug,
                            InitiativeId = initiativeId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateCommunityInitiative")
            .WithSummary(
                "Change an initiative; omitted fields are kept. Its organisers, or canManageInitiatives; "
                + "only canManageInitiatives changes the organisers.");

        group.MapPost("/{idOrSlug}/initiatives/{initiativeId:guid}/participation", async (
                string idOrSlug,
                Guid initiativeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new JoinCommunityInitiativeCommand { IdOrSlug = idOrSlug, InitiativeId = initiativeId },
                        cancellationToken)))
            .WithName("JoinCommunityInitiative")
            .WithSummary("Take part. Any current member, while it is Planned or Active. Joining twice is joining once.");

        group.MapDelete("/{idOrSlug}/initiatives/{initiativeId:guid}/participation", async (
                string idOrSlug,
                Guid initiativeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new LeaveCommunityInitiativeCommand { IdOrSlug = idOrSlug, InitiativeId = initiativeId },
                        cancellationToken)))
            .WithName("LeaveCommunityInitiative")
            .WithSummary("Stop taking part. Answers the initiative as it now stands.");

        group.MapGet("/{idOrSlug}/initiatives/{initiativeId:guid}/participants", async (
                string idOrSlug,
                Guid initiativeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityInitiativeParticipantsQuery
                        {
                            IdOrSlug = idOrSlug,
                            InitiativeId = initiativeId
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityInitiativeParticipants")
            .WithSummary(
                "Who is taking part, as each chose to be seen: Public to anybody, CommunityOnly to members.");

        group.MapGet("/{idOrSlug}/initiatives/{initiativeId:guid}/updates", async (
                string idOrSlug,
                Guid initiativeId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityInitiativeUpdatesQuery { IdOrSlug = idOrSlug, InitiativeId = initiativeId },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListCommunityInitiativeUpdates")
            .WithSummary("An initiative's progress updates, newest first.");

        group.MapPost("/{idOrSlug}/initiatives/{initiativeId:guid}/updates", async (
                string idOrSlug,
                Guid initiativeId,
                PostCommunityInitiativeUpdateRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PostCommunityInitiativeUpdateCommand
                        {
                            IdOrSlug = idOrSlug,
                            InitiativeId = initiativeId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("PostCommunityInitiativeUpdate")
            .WithSummary("Post a progress update. Its organisers, or canManageInitiatives; not once cancelled.");
    }
}
