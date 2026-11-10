using Diaspora.Modules.Guides.Application.Knowledge.Discovery;
using Diaspora.Modules.Guides.Application.Knowledge.Search;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Every way somebody arrives at a country's knowledge.
/// <para>
/// <b>None of it is authorised, and that is the whole product argument.</b> A knowledge base that
/// demands an account before it will tell somebody what to read about the country they are moving to
/// has no way of persuading them to make one. Every handler still applies visibility internally, so
/// an anonymous caller and a signed-in one see published knowledge and nothing else.
/// </para>
/// <para>
/// <b>The six shelf endpoints dispatch one query with a different shelf.</b> The brief names six
/// discovery queries; they differ in one predicate each, and six handlers would be five copies of
/// the same paging code waiting to drift. What a client consumes is six endpoints, which is what
/// matters to a client.
/// </para>
/// <para>
/// Filters come from the query string, never a body. A GET with a body is unusable from a browser,
/// uncacheable by any intermediary and unlinkable — and a discovery result that cannot be shared as
/// a URL is the one thing a knowledge base most needs to be shareable, because sharing is how the
/// answer reaches the person who needed it.
/// </para>
/// </summary>
internal static class KnowledgeDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapCountries(group);
        MapShelves(group);
        MapSearch(group);
    }

    private static void MapCountries(RouteGroupBuilder group)
    {
        group.MapGet("/countries", async (
                string? locale,
                bool? includeUnpublished,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListCountryDirectoriesQuery
                        {
                            Locale = locale,
                            IncludeUnpublished = includeUnpublished ?? false
                        },
                        cancellationToken)))
            .WithName("ListKnowledgeCountries")
            .WithSummary("Browse by country — every country the platform holds knowledge for.");

        group.MapGet("/countries/{countryCode}", async (
                string countryCode,
                string? locale,
                string? language,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCountryKnowledgeDirectoryQuery
                        {
                            CountryCode = countryCode,
                            Locale = locale,
                            Language = language
                        },
                        cancellationToken)))
            .WithName("GetCountryKnowledgeDirectory")
            .WithSummary("One country's knowledge directory — the whole country page.");

        group.MapGet("/countries/{countryCode}/dashboard", async (
                string countryCode,
                string? locale,
                string? language,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCountryDashboardQuery
                        {
                            CountryCode = countryCode,
                            Locale = locale,
                            Language = language
                        },
                        cancellationToken)))
            .WithName("GetCountryDashboard")
            .WithSummary("Seven shelves, the breakdown, trending topics and what else is here.");

        group.MapGet("/countries/{countryCode}/statistics", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetKnowledgeStatisticsQuery { CountryCode = countryCode },
                        cancellationToken)))
            .WithName("GetKnowledgeStatistics")
            .WithSummary("What exists for this country, and what people did with it.");

        group.MapGet("/countries/{countryCode}/categories/{categoryKey}", async (
                string countryCode,
                string categoryKey,
                string? locale,
                string? language,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetKnowledgeCategoryViewQuery
                        {
                            CountryCode = countryCode,
                            KnowledgeCategoryKey = categoryKey,
                            Locale = locale,
                            Language = language,
                            Limit = limit
                        },
                        cancellationToken)))
            .WithName("GetKnowledgeCategoryView")
            .WithSummary("One navigation row, opened: every shelf this deployment registers.");

        group.MapGet("/countries/{countryCode}/trending", async (
                string countryCode,
                int? limit,
                bool? unansweredOnly,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListTrendingTopicsQuery
                        {
                            CountryCode = countryCode,
                            Limit = limit,
                            UnansweredOnly = unansweredOnly ?? false
                        },
                        cancellationToken)))
            .WithName("ListKnowledgeTrendingTopics")
            .WithSummary("What people are searching for here. Unanswered phrases are curator-only.");
    }

    /// <summary>
    /// The six named shelves, plus the two the dashboard also exposes.
    /// <para>
    /// Separate routes rather than a <c>?shelf=</c> parameter, because they are different editorial
    /// promises and a URL that names one is a URL somebody can share: "the official guidance for
    /// Mauritius" is a link, and "the discovery endpoint with shelf=official" is a query somebody
    /// has to reconstruct.
    /// </para>
    /// </summary>
    private static void MapShelves(RouteGroupBuilder group)
    {
        MapShelf(group, "featured", KnowledgeShelf.Featured,
            "ListKnowledgeFeaturedGuides", "What a curator put at the top for this country.");

        MapShelf(group, "popular", KnowledgeShelf.Popular,
            "ListKnowledgePopularGuides", "Most read.");

        MapShelf(group, "recently-updated", KnowledgeShelf.RecentlyUpdated,
            "ListKnowledgeRecentlyUpdatedGuides", "Last edited — is any of this still true.");

        MapShelf(group, "beginner", KnowledgeShelf.Beginner,
            "ListKnowledgeBeginnerGuides", "Written for somebody who has just arrived.");

        MapShelf(group, "advanced", KnowledgeShelf.Advanced,
            "ListKnowledgeAdvancedGuides", "Written for somebody who has hit something specific.");

        MapShelf(group, "official", KnowledgeShelf.Official,
            "ListKnowledgeOfficialGuides", "Carrying an authority badge. Narrower than verified.");

        MapShelf(group, "verified", KnowledgeShelf.Verified,
            "ListKnowledgeVerifiedGuides", "Checked by Trust.");

        MapShelf(group, "community", KnowledgeShelf.Community,
            "ListKnowledgeCommunityGuides", "Written by members. Most of the knowledge here.");

        group.MapGet("/guides/{guideId:guid}/next", async (
                Guid guideId,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListRecommendedGuidesQuery { GuideId = guideId, Limit = limit },
                        cancellationToken)))
            .WithName("ListKnowledgeRecommendedReads")
            .WithSummary("What a curator says to read after this guide.");

        group.MapGet("/capabilities", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetKnowledgeCapabilitiesQuery(), cancellationToken)))
            .WithName("GetKnowledgeCapabilities")
            .WithSummary("What this deployment offers: sections, sorts, engine, cache, limits.");
    }

    private static void MapShelf(
        RouteGroupBuilder group, string route, KnowledgeShelf shelf, string name, string summary) =>
        group.MapGet($"/{route}", async (
                [AsParameters] BrowseKnowledgeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListKnowledgeShelfQuery { Shelf = shelf, Request = request },
                        cancellationToken)))
            .WithName(name)
            .WithSummary(summary);

    private static void MapSearch(RouteGroupBuilder group) =>
        group.MapGet("/search", async (
                [AsParameters] SearchKnowledgeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchKnowledgeQuery { Request = request }, cancellationToken)))
            .RequireRateLimiting(RateLimitPolicyNames.Search)
            .WithName("SearchKnowledge")
            .WithSummary("Search a country's organised knowledge, with every discovery filter.");
}
