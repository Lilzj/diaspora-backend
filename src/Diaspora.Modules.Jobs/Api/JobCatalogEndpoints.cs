using Diaspora.Modules.Jobs.Application.Catalog;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// The catalogues a client renders pickers from.
/// <para>
/// Unauthenticated, like the job reads. A member has to be able to see what employment domains
/// exist before they can browse, and a skill autocomplete that required a token would break the
/// search box for anonymous visitors.
/// </para>
/// </summary>
internal static class JobCatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/categories", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListJobCategoriesQuery(), cancellationToken)))
            .WithName("ListJobCategories")
            .WithSummary("The employment-domain catalogue.");

        group.MapGet("/skills", async (
                [FromQuery] string? q,
                [FromQuery] string? category,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SuggestSkillsQuery
                        {
                            Term = q,
                            CategoryKey = category,
                            Limit = limit ?? 20
                        },
                        cancellationToken)))
            .WithName("SuggestJobSkills")
            .WithSummary("Skill autocomplete, or the popular set when no term is given.");
    }
}
