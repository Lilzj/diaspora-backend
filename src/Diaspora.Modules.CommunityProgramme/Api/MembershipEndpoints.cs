using Diaspora.Modules.CommunityProgramme.Application.Memberships;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// COMMUNITY-LIFE-GAP-001 and -002: the membership programme and its fourteen-state lifecycle.
/// <para>
/// The routes are the register's, at the register's paths. <c>/memberships/discover</c> and
/// <c>/memberships/me</c> are mapped before <c>/memberships/{membershipId:guid}</c> so the literal
/// segments are not swallowed by the parameterised route.
/// </para>
/// </summary>
internal static class MembershipEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapDiscovery(group);
        MapMine(group);
        MapProgramme(group);
        MapAdministration(group);
    }

    private static void MapDiscovery(RouteGroupBuilder group) =>
        group.MapGet("/memberships/discover", async (
                [FromQuery] string? query,
                [FromQuery] string? category,
                [FromQuery] string? countryCode,
                [FromQuery] string? cityName,
                [FromQuery] Guid? neighborhoodAreaId,
                [FromQuery] string? priceBand,
                [FromQuery] bool? eligibleOnly,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DiscoverMembershipsQuery
                        {
                            Query = query,
                            Category = category,
                            CountryCode = countryCode,
                            CityName = cityName,
                            NeighborhoodAreaId = neighborhoodAreaId,
                            PriceBand = priceBand,
                            EligibleOnly = eligibleOnly ?? false,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("DiscoverMemberships")
            .WithSummary("Membership programmes a member could join.");

    private static void MapMine(RouteGroupBuilder group)
    {
        group.MapGet("/memberships/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyMembershipsQuery(), cancellationToken)))
            .WithName("ListMyProgrammeMemberships")
            .WithSummary("Every membership the caller holds, terminal ones included.");

        group.MapGet("/memberships/{membershipId:guid}", async (
                Guid membershipId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMembershipQuery { MembershipId = membershipId },
                        cancellationToken)))
            .WithName("GetProgrammeMembership")
            .WithSummary("One membership, with its history.");

        group.MapPost("/memberships/{membershipId:guid}/cancel", async (
                Guid membershipId,
                CancelMembershipRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelMembershipCommand
                        {
                            MembershipId = membershipId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("CancelProgrammeMembership")
            .WithSummary("The member ends their own membership.");

        group.MapPost("/memberships/{membershipId:guid}/renew", async (
                Guid membershipId,
                RenewMembershipRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RenewMembershipCommand
                        {
                            MembershipId = membershipId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("RenewProgrammeMembership")
            .WithSummary("Extends a membership by another billing period. Idempotent.");

        // COMMUNITY-LIFE-GAP-003. The route is this module's because the client holds a membership
        // identifier and this module owns the membership; every financial decision behind it is
        // B15's, dispatched as B15's own command. See RecordMembershipSettlementCommand.
        group.MapPost("/memberships/{membershipId:guid}/payments", async (
                Guid membershipId,
                Contracts.V1.Requests.BindMembershipPaymentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RecordMembershipSettlementCommand
                        {
                            MembershipId = membershipId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("BindMembershipPayment")
            .WithSummary(
                "Binds a settled B15 payment to a membership and activates it. Refuses anything "
                + "that has not actually settled.");

        group.MapPost("/memberships/{membershipId:guid}/decision", async (
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
                            MembershipId = membershipId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("DecideProgrammeMembership")
            .WithSummary("An organization's decision about a member. Role-gated.");
    }

    private static void MapProgramme(RouteGroupBuilder group)
    {
        group.MapGet("/{organizationId:guid}/membership", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMembershipProgrammeQuery { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("GetMembershipProgramme")
            .WithSummary("An organization's membership programme, with the caller's own standing.");

        group.MapGet("/{organizationId:guid}/membership/tiers", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMembershipTiersQuery { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("ListMembershipTiers")
            .WithSummary("An organization's tiers, with the caller's requirement evaluation.");

        group.MapPut("/{organizationId:guid}/membership", async (
                Guid organizationId,
                SaveMembershipProgrammeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SaveMembershipProgrammeCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SaveMembershipProgramme")
            .WithSummary("Opens or rewrites an organization's membership programme. Role-gated.");

        group.MapPost("/{organizationId:guid}/memberships/applications", async (
                Guid organizationId,
                SubmitMembershipApplicationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SubmitMembershipApplicationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request,
                            IdempotencyKeyHeader = IdempotencyHeader.From(context)
                        },
                        cancellationToken)))
            .WithName("SubmitMembershipApplication")
            .WithSummary("Applies for a membership. Idempotent on the key.");
    }

    private static void MapAdministration(RouteGroupBuilder group) =>
        group.MapGet("/{organizationId:guid}/memberships", async (
                Guid organizationId,
                [FromQuery] string? state,
                [FromQuery] string? cursor,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListOrganizationMembersQuery
                        {
                            OrganizationId = organizationId,
                            State = state,
                            Cursor = cursor,
                            Take = take
                        },
                        cancellationToken)))
            .WithName("ListOrganizationProgrammeMembers")
            .WithSummary("An organization's member roll. Role-gated, paged.");
}
