using Diaspora.Modules.Marketplace.Application.Catalog;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// The commerce catalogues.
/// <para>
/// The two reads are anonymous, because a client cannot render a browse screen or a create form
/// without them and both are needed before anybody signs in. The two writes are administrative.
/// </para>
/// </summary>
internal static class MarketplaceCatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/categories", async (
                [FromQuery] bool? includeInactive,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCategoriesQuery
                        {
                            IncludeInactive = includeInactive ?? false
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListMarketplaceCategories")
            .WithSummary("The commerce taxonomy, flat with materialised paths.");

        group.MapGet("/categories/{key}/attributes", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCategoryAttributesQuery { CategoryKey = key },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListMarketplaceCategoryAttributes")
            .WithSummary("The structured fields a category expects. The create form is built from this.");

        group.MapPost("/categories", async (
                [FromBody] CreateCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateCategoryCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ManageCategories)
            .WithName("CreateMarketplaceCategory")
            .WithSummary("Add a node to the taxonomy.");

        group.MapPut("/categories/{key}", async (
                string key,
                [FromBody] UpdateCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCategoryCommand { Key = key, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ManageCategories)
            .WithName("UpdateMarketplaceCategory")
            .WithSummary("Rename or retire a category. The key never moves.");
    }
}
