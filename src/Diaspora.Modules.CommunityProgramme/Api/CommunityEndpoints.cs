using Diaspora.Modules.CommunityProgramme.Application.Communities;
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
    }
}
