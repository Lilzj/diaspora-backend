using Diaspora.Modules.Location.Application.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>The hierarchy's routes: nodes, their children, and the neighbourhood hub.</summary>
internal static class AreaEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/areas/{areaId:guid}", async (
                Guid areaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAreaQuery { AreaId = areaId }, cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("GetArea")
            .WithSummary("One node of the geographic hierarchy, with its breadcrumb.");

        group.MapGet("/areas/{areaId:guid}/children", async (
                Guid areaId,
                string? level,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListAreaChildrenQuery
                    {
                        AreaId = areaId,
                        Level = LocationRouteParsing.TryParse<AdministrativeLevel>(level),
                        Pagination = LocationRouteParsing.Paginate(page, pageSize)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("ListAreaChildren")
            .WithSummary("Browse one rung down the hierarchy.");

        group.MapGet("/areas/{areaId:guid}/ancestors", async (
                Guid areaId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetAreaQuery { AreaId = areaId }, cancellationToken);

                // The detail response already carries the breadcrumb, so the ancestors route is
                // a projection of the same query rather than a second one. One code path means
                // the breadcrumb cannot differ between the two endpoints that serve it.
                return result.IsSuccess
                    ? PlatformResults.Ok(
                        context, SharedKernel.Results.Result.Success(result.Value.Ancestors))
                    : PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("GetAreaAncestors")
            .WithSummary("A node's ancestors, root first.");

        // The neighbourhood hub. Resolves the slug the frontend's NeighborhoodCard already
        // builds, so existing links keep working against the real backend.
        group.MapGet("/neighborhoods/{idOrSlug}", async (
                string idOrSlug,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetNeighborhoodHubQuery
                    {
                        IdOrSlug = idOrSlug,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("GetNeighborhoodHub")
            .WithSummary("A neighbourhood's own page: indexes, resource counts and breadcrumb.");
    }
}
