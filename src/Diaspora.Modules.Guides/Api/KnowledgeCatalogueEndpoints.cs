using Diaspora.Modules.Guides.Application.Knowledge.Categories;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The navigation catalogue — the shape of the whole knowledge product.
/// <para>
/// The read is public and the writes are administrator-only. That split is the same one B8 makes for
/// its filing catalogue and for the same reason: a chip row every anonymous client fetches and a
/// taxonomy editor are not the same surface, and folding them together is how a permission stops
/// being obvious.
/// </para>
/// <para>
/// <b>The key is the address and the address is immutable.</b> It is what a contributor seed
/// declares, what a URL carries and what every country directory entry points at — so it is the
/// route value, an update cannot rewrite it, and retiring a row is an archival rather than a delete.
/// </para>
/// </summary>
internal static class KnowledgeCatalogueEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/categories", async (
                string? locale,
                bool? includeArchived,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListKnowledgeCategoriesQuery
                        {
                            Locale = locale,
                            IncludeArchived = includeArchived ?? false
                        },
                        cancellationToken)))
            .WithName("ListKnowledgeCategories")
            .WithSummary("The navigation catalogue, ordered. Archived rows are curator-only.");

        group.MapPost("/categories", async (
                CreateKnowledgeCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateKnowledgeCategoryCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageCategories)
            .WithName("CreateKnowledgeCategory")
            .WithSummary("Add a navigation row. Its key is the contract and can never change.");

        group.MapPut("/categories/{key}", async (
                string key,
                UpdateKnowledgeCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateKnowledgeCategoryCommand { Key = key, Request = request },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageCategories)
            .WithName("UpdateKnowledgeCategory")
            .WithSummary("Rewrite a navigation row. Collections are replaced, not patched.");

        group.MapDelete("/categories/{key}", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveKnowledgeCategoryCommand { Key = key }, cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageCategories)
            .WithName("ArchiveKnowledgeCategory")
            .WithSummary("Retire a row. Refused while a country still lists it.");

        group.MapPost("/categories/{key}/restore", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreKnowledgeCategoryCommand { Key = key }, cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageCategories)
            .WithName("RestoreKnowledgeCategory")
            .WithSummary("Return an archived row to the catalogue.");
    }
}
