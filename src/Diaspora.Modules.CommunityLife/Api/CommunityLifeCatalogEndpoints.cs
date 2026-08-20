using Diaspora.Modules.CommunityLife.Application.Catalog;
using Diaspora.Modules.CommunityLife.Application.Discovery;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The catalogue: one read for the client, two writes for staff.
/// <para>
/// <b>One read rather than three.</b> A create form needs the types, the categories and the facets
/// before it can render a single dropdown, and three round trips would be three chances to render
/// half a form.
/// </para>
/// </summary>
internal static class CommunityLifeCatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/catalogue", async (
                [FromQuery] bool? includeRetired,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityLifeCatalogueQuery
                        {
                            IncludeRetired = includeRetired ?? false
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityLifeCatalogue")
            .WithSummary("The organization types, event categories and facets.");

        group.MapPut("/catalogue/organization-types", async (
                UpsertOrganizationTypeCommand command,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(context, await sender.Send(command, cancellationToken)))
            .WithName("UpsertOrganizationType")
            .WithSummary("Adds an organization type or restates one. Staff only.");

        group.MapPut("/catalogue/event-categories", async (
                UpsertEventCategoryCommand command,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(context, await sender.Send(command, cancellationToken)))
            .WithName("UpsertEventCategory")
            .WithSummary("Adds an event category or restates one. Staff only.");
    }
}
