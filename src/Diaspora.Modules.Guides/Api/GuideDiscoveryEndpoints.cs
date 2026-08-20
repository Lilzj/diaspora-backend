using Diaspora.Modules.Guides.Application.Capabilities;
using Diaspora.Modules.Guides.Application.Categories;
using Diaspora.Modules.Guides.Application.Discovery;
using Diaspora.Modules.Guides.Application.Tags;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Every way a reader arrives at a guide they have not read yet.
/// <para>
/// <b>None of it is authorised, and that is deliberate.</b> A knowledge base that demands an
/// account before it will answer "how do I register a residence permit" has no way of persuading
/// anybody to make one. Every handler here still applies visibility internally, so an anonymous
/// caller and a signed-in one see published guides and nothing else.
/// </para>
/// <para>
/// <b>Filters come from the query string, never a body.</b> A GET with a body is unusable from a
/// browser, uncacheable by any intermediary and unlinkable — and a discovery result that cannot
/// be shared as a URL is the one thing a knowledge base most needs to be shareable, because
/// sharing is how the knowledge actually reaches the person who needed it.
/// </para>
/// <para>
/// The shelves are separate queries rather than flags on search because they are different
/// editorial promises: featured is what staff chose, trending is what is earning attention now,
/// latest is the only ordering with no judgement in it, and nearby is the answer for where the
/// caller actually is.
/// </para>
/// </summary>
internal static class GuideDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapSearch(group);
        MapShelves(group);
        MapBrowse(group);
        MapCatalogue(group);
    }

    private static void MapSearch(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
                [AsParameters] SearchGuidesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchGuidesQuery { Request = request }, cancellationToken)))
            .WithName("SearchGuides")
            .WithSummary("Find guides, with every discovery filter the client offers.");
    }

    private static void MapShelves(RouteGroupBuilder group)
    {
        group.MapGet("/featured", async (
                string? countryCode,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListFeaturedGuidesQuery
                        {
                            CountryCode = countryCode,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListFeaturedGuides")
            .WithSummary("The featured carousel — what staff chose to put in front of everybody.");

        group.MapGet("/latest", async (
                string? countryCode,
                string? language,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListLatestGuidesQuery
                        {
                            CountryCode = countryCode,
                            Language = language,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListLatestGuides")
            .WithSummary("What was published most recently.");

        group.MapGet("/trending", async (
                string? countryCode,
                string? categoryKey,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListTrendingGuidesQuery
                        {
                            CountryCode = countryCode,
                            CategoryKey = categoryKey,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListTrendingGuides")
            .WithSummary("What is earning attention now, as opposed to what earned it once.");

        group.MapGet("/nearby", async (
                [AsParameters] ListNearbyGuidesRequest request,
                string? cursor,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListNearbyGuidesQuery { Request = request, Cursor = cursor },
                        cancellationToken)))
            .WithName("ListNearbyGuides")
            .WithSummary("Guides for where the caller is, in whatever terms the client has.");
    }

    private static void MapBrowse(RouteGroupBuilder group)
    {
        group.MapGet("/countries/{countryCode}", async (
                string countryCode,
                string? categoryKey,
                string? language,
                string? sort,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuidesByCountryQuery
                        {
                            CountryCode = countryCode,
                            CategoryKey = categoryKey,
                            Language = language,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListGuidesByCountry")
            .WithSummary("Everything published about one country.");

        group.MapGet("/categories/{categoryKey}/guides", async (
                string categoryKey,
                string? countryCode,
                string? language,
                string? sort,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuidesByCategoryQuery
                        {
                            CategoryKey = categoryKey,
                            CountryCode = countryCode,
                            Language = language,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListGuidesByCategory")
            .WithSummary("Everything published under one catalogue category, filed by its key.");

        group.MapGet("/authors/{authorId:guid}", async (
                Guid authorId,
                string? sort,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuidesByAuthorQuery
                        {
                            AuthorId = authorId,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListGuidesByAuthor")
            .WithSummary("One author's published guides — the byline a grateful reader clicks.");

        group.MapGet("/{guideId:guid}/translations", async (
                Guid guideId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuideTranslationsQuery { GuideId = guideId }, cancellationToken)))
            .WithName("ListGuideTranslations")
            .WithSummary("The other language editions — what the language switcher is built from.");
    }

    private static void MapCatalogue(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (
                string? parentKey,
                bool? includeInactive,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuideCategoriesQuery
                        {
                            ParentKey = parentKey,
                            IncludeInactive = includeInactive ?? false
                        },
                        cancellationToken)))
            .WithName("ListGuideCategories")
            .WithSummary("The guide catalogue, flat and ordered.");

        group.MapGet("/tags", async (
                string? countryCode,
                string? categoryKey,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuideTagsQuery
                        {
                            CountryCode = countryCode,
                            CategoryKey = categoryKey,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("ListGuideTags")
            .WithSummary("The tag cloud, built from what authors actually wrote about.");

        group.MapGet("/capabilities", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetGuideCapabilitiesQuery(), cancellationToken)))
            .WithName("GetGuideCapabilities")
            .WithSummary("What this deployment offers: block kinds, sort keys, strategies, limits.");
    }
}
