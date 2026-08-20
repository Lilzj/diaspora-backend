using Diaspora.Modules.CommunityProgramme.Application.Organizations;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// COMMUNITY-LIFE-GAP-013's surface: the caller's organizations, their capabilities, the dashboard,
/// and role assignment.
/// <para>
/// Every route here is a read except the last, and the last is a write B12 performs. See
/// <see cref="AssignOrganizationRoleCommand"/> for why the route is B18's and the write is not.
/// </para>
/// </summary>
internal static class OrganizationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/organizations/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListMyOrganizationsQuery(), cancellationToken)))
            .WithName("ListMyCommunityOrganizations")
            .WithSummary("The caller's organizations, with the role and capability set for each.");

        group.MapGet("/organizations/{organizationId:guid}/capabilities", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOrganizationCapabilitiesQuery { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("GetOrganizationCapabilities")
            .WithSummary("What the caller may do inside one organization. Served as data.");

        group.MapGet("/organizations/{organizationId:guid}/dashboard", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOrganizationDashboardQuery { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("GetOrganizationProgrammeDashboard")
            .WithSummary("The dashboard's first paint: capabilities and section counts, in one call.");

        group.MapPost("/organizations/{organizationId:guid}/members/{userId:guid}/role", async (
                Guid organizationId,
                Guid userId,
                AssignOrganizationRoleRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AssignOrganizationRoleCommand
                        {
                            OrganizationId = organizationId,
                            UserId = userId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("AssignCommunityOrganizationRole")
            .WithSummary("Assigns an organization role. Dispatched to B12, which owns the edge.");
    }
}
