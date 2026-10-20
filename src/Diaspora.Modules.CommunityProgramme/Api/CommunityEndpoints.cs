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
/// FRONTEND-PHASE13-GAP-001 and -002: the community as a first-class entity.
/// <para>
/// <b>These two routes sit at the root of the module's prefix, and that placement is load-bearing.</b>
/// <c>GET /api/v1/communities</c> and <c>GET /api/v1/communities/{idOrSlug}</c> are exactly the paths
/// the client's register declares — the prefix was chosen for them before either existed.
/// </para>
/// <para>
/// <b>The profile route is a single parameterised segment beside eight literal ones.</b> ASP.NET
/// Core prefers the literal, so a community whose handle is <c>campaigns</c> would resolve to the
/// campaign list for ever, silently. <see cref="Domain.Communities.CommunitySlug"/> refuses those
/// handles, and <c>ApiGuardrailTests.No_route_segment_under_communities_can_be_taken_as_a_community_handle</c>
/// reads the composed route table and fails when somebody maps a ninth without reserving it. That
/// test found three missing words on the day it was written.
/// </para>
/// <para>
/// <b>Both are anonymous.</b> The register requires it: browsing communities is what somebody does
/// before they have an account, and an unauthenticated caller gets public communities with
/// <c>membershipState: "None"</c> rather than a 401. Visibility is decided per row in the handlers,
/// which is the only place the caller is known.
/// </para>
/// </summary>
internal static class CommunityEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // ── The directory ───────────────────────────────────────────────────────────────────
        //
        // Every parameter here is bound, and the shapes are the client's rather than this
        // platform's preference. `types` and `originCountryCodes` are single comma-joined strings
        // because that is what the client sends; declaring them `string[]` would bind repeated
        // keys, take "A,B" as one element, match nothing, and serve an unfiltered browse under a
        // lit filter chip — the model binder discards what it cannot bind without an error, and
        // this platform has shipped that exact defect four times in one file.
        group.MapGet("/", async (
                [FromQuery] string? query,
                [FromQuery] string? types,
                [FromQuery] string? originCountryCodes,
                [FromQuery] string? presenceCountryCode,
                [FromQuery] bool? includeJoined,
                [FromQuery] string? sort,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityDirectoryQuery
                        {
                            Query = query,
                            Types = types,
                            OriginCountryCodes = originCountryCodes,
                            PresenceCountryCode = presenceCountryCode,
                            IncludeJoined = includeJoined ?? true,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityDirectory")
            .WithSummary("The communities on the platform, with the caller's standing in each.");

        // ── One community ───────────────────────────────────────────────────────────────────
        //
        // No `:guid` constraint, because the segment is deliberately either. The repository parses
        // a Guid as an identifier and everything else as a handle; a handle can never look like a
        // Guid, since the slug scheme has no braces and no dashes in that arrangement.
        group.MapGet("/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityQuery { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunity")
            .WithSummary("One community, by identifier or by handle.");

        MapMembership(group);
    }

    /// <summary>
    /// The member's own standing — GAP-003, -004, -005 and -006.
    /// <para>
    /// <b>The set and the primary live under <c>/me</c>, and neither is where the register sketched
    /// it.</b> <c>/communities/memberships/me</c> is taken: it has answered B18's <em>programme</em>
    /// memberships since the module shipped, a different concept wearing the same word, and the
    /// register marked GAP-003 connected against it. <c>/users/me/primary-community</c> is worse —
    /// there is no <c>/api/v1/users</c> space on this platform and no module owns one, so the
    /// register sketched a route into a prefix that does not exist.
    /// </para>
    /// <para>
    /// <c>me</c> is a reserved handle, so nothing under <c>/communities/me/</c> can ever be shadowed
    /// by a community, and the guardrail over the route table keeps that true.
    /// </para>
    /// </summary>
    private static void MapMembership(RouteGroupBuilder group)
    {
        // Before /{idOrSlug}/… in the file so the pair reads together. Routing resolves by
        // specificity rather than by registration order, and `me` being reserved is what actually
        // protects it.
        group.MapGet("/me/memberships", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyCommunityMembershipsQuery(), cancellationToken)))
            .WithName("GetMyCommunityMemberships")
            .WithSummary("Every community relationship the caller holds, terminal ones included.");

        group.MapPut("/me/primary", async (
                SetPrimaryCommunityRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetPrimaryCommunityCommand { CommunityId = request?.CommunityId },
                        cancellationToken)))
            .WithName("SetPrimaryCommunity")
            .WithSummary("Leads with one community, or clears it. Answers the whole set.");

        group.MapGet("/{idOrSlug}/capabilities", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityCapabilitiesQuery { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .AllowAnonymous()

            // Not "GetCommunityCapabilities": B6 has held that name since it shipped, on
            // GET /api/v1/community/capabilities, which serves its social feature catalogue. Endpoint
            // names are globally unique, and a duplicate is not a startup failure — it throws on the
            // first request that builds the route table, as a 500 on an unrelated URL. The same
            // collision made this module's capability record CommunityMemberCapabilitiesResponse.
            .WithName("GetCommunityMemberCapabilities")
            .WithSummary("What the caller may do here. All false rather than a refusal.");

        // The act is a body field rather than a path segment, which is what the client already
        // sends: its mutation hooks take the action as data rather than as four functions.
        group.MapPost("/{idOrSlug}/membership", async (
                string idOrSlug,
                ChangeCommunityMembershipRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ChangeCommunityMembershipCommand
                        {
                            IdOrSlug = idOrSlug,
                            Action = request?.Action
                        },
                        cancellationToken)))
            .WithName("ChangeCommunityMembership")
            .WithSummary("Join, follow or express interest in a community.");

        // The act rides in the query string, not a body.
        //
        // The client's `write` helper passes `data` on a delete, so a body was the obvious shape and
        // it does not work: minimal APIs refuse to *infer* a body parameter on DELETE, and the host
        // throws "Body was inferred but the method does not allow inferred body parameters" at
        // startup. `[FromBody]` would silence that, and a query parameter is the better answer
        // anyway — a body on DELETE is legal but is dropped by enough proxies that an action lost in
        // transit would read as the default, and the default here is `leave`.
        //
        // The client sends `?action=…` instead. That is the one change this route asks of it.
        group.MapDelete("/{idOrSlug}/membership", async (
                string idOrSlug,
                [FromQuery] string? action,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new EndCommunityMembershipCommand
                        {
                            IdOrSlug = idOrSlug,
                            Action = action
                        },
                        cancellationToken)))
            .WithName("EndCommunityMembership")
            .WithSummary("Leave a community, unfollow it, or withdraw an interest.");

        // The opt-in half of §41. Without it the Private default is a dead end and the column is
        // unwritable by anybody, which is not a privacy model.
        group.MapPut("/{idOrSlug}/membership/visibility", async (
                string idOrSlug,
                SetMembershipVisibilityRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetMembershipVisibilityCommand
                        {
                            IdOrSlug = idOrSlug,
                            Visibility = request?.Visibility
                        },
                        cancellationToken)))
            .WithName("SetCommunityMembershipVisibility")
            .WithSummary("Who may see the caller belongs here. Set by the member alone.");

        // ── Administration ──────────────────────────────────────────────────────────────────
        //
        // Founding, roles, and the two acts staff keep. The rules are in CommunityAdministration.cs;
        // the short version is that the founder owns the community, the owner and administrators
        // give roles, only the owner gives Administrator, and nobody is made Owner through a route
        // a member can call.

        group.MapPost("/", async (
                ProposeCommunityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ProposeCommunityCommand { Request = request }, cancellationToken)))
            .WithName("ProposeCommunity")
            .WithSummary(
                "Found a community. The caller becomes its owner and an administrator. It opens "
                + "pending, and nobody else can join until it is opened.");

        group.MapGet("/{idOrSlug}/members", async (
                string idOrSlug,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityMembersQuery
                        {
                            IdOrSlug = idOrSlug,
                            Cursor = cursor,
                            Limit = limit ?? 25
                        },
                        cancellationToken)))
            .WithName("ListCommunityMembers")
            .WithSummary(
                "Members the caller may see: those who made their membership visible, and the caller. "
                + "For members only.");

        group.MapPut("/{idOrSlug}/members/{userId:guid}/roles", async (
                string idOrSlug,
                Guid userId,
                ChangeCommunityRoleRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ChangeCommunityRoleCommand
                        {
                            IdOrSlug = idOrSlug,
                            UserId = userId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("ChangeCommunityMemberRole")
            .WithSummary(
                "Grant or revoke Moderator, Ambassador or Administrator. Administrator is the owner's "
                + "to give; the others are any administrator's.");

        group.MapPost("/{idOrSlug}/open", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new OpenCommunityCommand { IdOrSlug = idOrSlug }, cancellationToken)))
            .WithName("OpenCommunity")
            .WithSummary("Open a pending community into the directory. Platform administrators only.");

        group.MapPut("/{idOrSlug}/owner", async (
                string idOrSlug,
                AppointCommunityOwnerRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AppointCommunityOwnerCommand
                        {
                            IdOrSlug = idOrSlug,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("AppointCommunityOwner")
            .WithSummary(
                "Make a member the owner of a community that has none — the curated ones nobody "
                + "founded. Platform administrators only.");

        // ── Closing a community, and handing one over — 11 Sep 2026 ────────────────
        //
        // The staff routes are literal segments (`/deletion-requests`), which routing prefers over
        // `{idOrSlug}`, so a community can never be mistaken for the queue.

        group.MapPost("/{idOrSlug}/deletion-request", async (
                string idOrSlug,
                RequestCommunityDeletionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RequestCommunityDeletionCommand
                        {
                            IdOrSlug = idOrSlug,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("RequestCommunityDeletion")
            .WithSummary(
                "Ask Diaspora to close this community. The owner's alone, needs a reason, and "
                + "performs nothing: it waits thirty days while a Diaspora administrator decides.");

        group.MapDelete("/{idOrSlug}/deletion-request", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawCommunityDeletionCommand { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .WithName("WithdrawCommunityDeletion")
            .WithSummary("Change your mind. The owner's, at any point before the community closes.");

        group.MapGet("/{idOrSlug}/deletion-request", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityDeletionRequestQuery { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .WithName("GetCommunityDeletionRequest")
            .WithSummary(
                "What is happening to this community, if anything. Whoever runs it, and Diaspora — "
                + "not its members, who are told nothing while the wait runs.");

        group.MapGet("/deletion-requests", async (
                string? status,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCommunityDeletionRequestsQuery
                        {
                            Status = status,
                            Cursor = cursor,
                            Limit = limit ?? 20
                        },
                        cancellationToken)))
            .WithName("ListCommunityDeletionRequests")
            .WithSummary("The review queue. Platform administrators only.");

        group.MapPost("/deletion-requests/{requestId:guid}/decision", async (
                Guid requestId,
                DecideCommunityDeletionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideCommunityDeletionCommand
                        {
                            RequestId = requestId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideCommunityDeletion")
            .WithSummary(
                "Approve or refuse a request to close a community. Platform administrators only. A "
                + "refusal needs a reason, which the owner is shown word for word.");

        group.MapPost("/{idOrSlug}/ownership-offers", async (
                string idOrSlug,
                OfferCommunityOwnershipRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new OfferCommunityOwnershipCommand
                        {
                            IdOrSlug = idOrSlug,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("OfferCommunityOwnership")
            .WithSummary(
                "Offer the community to one of its moderators or administrators. The owner's alone, "
                + "and it is an offer: they accept or refuse.");

        group.MapDelete("/{idOrSlug}/ownership-offers", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawCommunityOwnershipOfferCommand { IdOrSlug = idOrSlug },
                        cancellationToken)))
            .WithName("WithdrawCommunityOwnershipOffer")
            .WithSummary("Take back an offer nobody has answered yet.");

        group.MapGet("/ownership-offers/mine", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyCommunityOwnershipOffersQuery(), cancellationToken)))
            .WithName("ListMyCommunityOwnershipOffers")
            .WithSummary("Communities somebody has asked you to take over.");

        group.MapPost("/ownership-offers/{transferId:guid}/decision", async (
                Guid transferId,
                DecideCommunityOwnershipRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideCommunityOwnershipCommand
                        {
                            TransferId = transferId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideCommunityOwnership")
            .WithSummary(
                "Accept or refuse a community somebody offered you. Only the person it was offered "
                + "to may answer. Accepting makes you its owner.");

        // ── The community's own moderation queue — GAP-017, 21 Oct 2026 ─────────────────────
        //
        // Reports about what was published as this community, for its moderators and
        // administrators. The platform-wide queue stays at /community/moderation/queue for staff.
        //
        // The body is a bare array because that is what the client reads. The next page's cursor
        // rides in a header rather than wrapping the array, so the client's shape holds and a
        // client that pages can still find where to start.
        group.MapGet("/{idOrSlug}/reports", async (
                string idOrSlug,
                string? status,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var page = await sender.Send(
                    new GetCommunityReportsQuery
                    {
                        IdOrSlug = idOrSlug,
                        Status = status,
                        Cursor = cursor,
                        Limit = limit ?? CommunityModerationAccess.DefaultLimit
                    },
                    cancellationToken);

                if (page.IsSuccess && page.Value.NextCursor is { } next)
                {
                    context.Response.Headers[NextCursorHeader] = next;
                }

                return PlatformResults.Ok(context, page.Map(value => value.Items));
            })
            .WithName("ListCommunityReports")
            .WithSummary(
                "Reports about this community's content, oldest first, one row per piece of "
                + "content. Moderators and administrators of the community. No reporter identity. "
                + "The next page's cursor is in the X-Next-Cursor header.");

        group.MapPost("/{idOrSlug}/reports/{reportId:guid}/decision", async (
                string idOrSlug,
                Guid reportId,
                DecideCommunityReportRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideCommunityReportCommand
                        {
                            IdOrSlug = idOrSlug,
                            ReportId = reportId,
                            Decision = request?.Decision,
                            Note = request?.Note
                        },
                        cancellationToken)))
            .WithName("DecideCommunityReport")
            .WithSummary(
                "Decide a report about this community's content: dismiss, remove-content or "
                + "escalate. Answers every open report about the same content. A report about "
                + "anything else is not found.");
    }

    /// <summary>Where the next page of a community's report queue starts. Absent on the last page.</summary>
    public const string NextCursorHeader = "X-Next-Cursor";
}
