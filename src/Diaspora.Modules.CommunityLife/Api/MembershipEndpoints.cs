using Diaspora.Modules.CommunityLife.Application.Memberships;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The membership surface, nested under the organization it belongs to.
/// <para>
/// Nested rather than flat, because every question about a membership is a question about which
/// organization it is in — and a flat <c>/memberships/{id}</c> would let a client act on one without
/// naming the body, which is exactly the check every handler here starts with.
/// </para>
/// </summary>
internal static class MembershipEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/organizations/{organizationId:guid}/members", async (
                Guid organizationId,
                [FromQuery(Name = "status")] string[]? statuses,
                [FromQuery(Name = "minimumRole")] string? minimumRole,
                [FromQuery(Name = "q")] string? searchTerm,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMembershipsQuery
                        {
                            OrganizationId = organizationId,
                            Statuses = statuses,
                            MinimumRole = minimumRole,
                            SearchTerm = searchTerm,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListOrganizationMembers")
            .WithSummary("An organization's members.");

        group.MapGet("/memberships/mine", async (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyMembershipsQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .WithName("ListMyMemberships")
            .WithSummary("The caller's own memberships, pending ones included.");

        group.MapPost("/organizations/{organizationId:guid}/members", async (
                Guid organizationId,
                JoinOrganizationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new JoinOrganizationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request ?? new JoinOrganizationRequest()
                        },
                        cancellationToken)))
            .WithName("JoinOrganization")
            .WithSummary("Asks to join an organization, or joins it outright.");

        group.MapPost("/organizations/{organizationId:guid}/members/invitations", async (
                Guid organizationId,
                InviteMemberRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new InviteMemberCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("InviteOrganizationMember")
            .WithSummary("Invites somebody into an organization.");

        MapDecisions(group);
    }

    private static void MapDecisions(RouteGroupBuilder group)
    {
        group.MapPost(
                "/organizations/{organizationId:guid}/members/{membershipId:guid}/decision",
                async (
                    Guid organizationId,
                    Guid membershipId,
                    DecideMembershipRequest request,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(
                            new DecideMembershipCommand
                            {
                                OrganizationId = organizationId,
                                MembershipId = membershipId,
                                Request = request
                            },
                            cancellationToken)))
            .WithName("DecideMembership")
            .WithSummary("Approves or refuses a pending membership.");

        group.MapPut(
                "/organizations/{organizationId:guid}/members/{membershipId:guid}/role",
                async (
                    Guid organizationId,
                    Guid membershipId,
                    ChangeMemberRoleRequest request,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(
                            new ChangeMemberRoleCommand
                            {
                                OrganizationId = organizationId,
                                MembershipId = membershipId,
                                Request = request
                            },
                            cancellationToken)))
            .WithName("ChangeMemberRole")
            .WithSummary("Promotes or stands down a member.");

        group.MapDelete(
                "/organizations/{organizationId:guid}/members/{membershipId:guid}",
                async (
                    Guid organizationId,
                    Guid membershipId,
                    [FromQuery] string? reason,
                    ISender sender,
                    HttpContext context,
                    CancellationToken cancellationToken) =>
                    PlatformResults.Ok(
                        context,
                        await sender.Send(
                            new RemoveMemberCommand
                            {
                                OrganizationId = organizationId,
                                MembershipId = membershipId,
                                Request = new RemoveMemberRequest { Reason = reason }
                            },
                            cancellationToken)))
            .WithName("RemoveOrganizationMember")
            .WithSummary("Removes somebody from an organization.");

        // Separate from the removal route, and not merely a convenience: leaving is the member's
        // own act and requires no role, whereas removing somebody requires outranking them.
        group.MapDelete("/organizations/{organizationId:guid}/members/me", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new LeaveOrganizationCommand { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("LeaveOrganization")
            .WithSummary("The caller walks away from an organization.");
    }
}
