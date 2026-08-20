using Diaspora.Modules.Guides.Application.Categories;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Administering the guide catalogue — the shape of the whole knowledge base.
/// <para>
/// Kept apart from the public catalogue read in <c>GuideDiscoveryEndpoints</c> on purpose: these
/// three routes are role-scoped to administrators under
/// <see cref="GuidePolicies.ManageCategories"/>, and folding a taxonomy editor in beside the chip
/// row every anonymous client fetches is how a permission stops being obvious.
/// </para>
/// <para>
/// <b>The key is the address and the address is immutable.</b> It is what a contributor seed
/// declares, what a URL carries and what every guide filed under it stores — so it is the route
/// value, an update cannot rewrite it, and retiring a category is a deactivation rather than a
/// delete, because guides filed under it were filed correctly at the time.
/// </para>
/// </summary>
internal static class GuideCategoryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/categories", async (
                CreateGuideCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateGuideCategoryCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.ManageCategories)
            .WithName("CreateGuideCategory")
            .WithSummary("Add a category. Its key is the contract and can never be changed.");

        group.MapPut("/categories/{key}", async (
                string key,
                UpdateGuideCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateGuideCategoryCommand { Key = key, Request = request },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.ManageCategories)
            .WithName("UpdateGuideCategory")
            .WithSummary("Rename or re-describe a catalogue entry, addressed by its stable key.");

        group.MapDelete("/categories/{key}", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeactivateGuideCategoryCommand { Key = key }, cancellationToken)))
            .RequireAuthorization(GuidePolicies.ManageCategories)
            .WithName("DeactivateGuideCategory")
            .WithSummary("Retire a category from the picker. Never deleted — guides store its key.");
    }
}
