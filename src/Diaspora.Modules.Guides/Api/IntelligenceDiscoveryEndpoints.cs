using Diaspora.Modules.Guides.Application.Intelligence.Discovery;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The reads a member makes: a country's whole intelligence layer, search, and what this deployment
/// can do.
/// <para>
/// <b>All anonymous.</b> Reading a country's official information requires no account, and an
/// emergency number in particular must be reachable by somebody who has never signed in and is not
/// about to. The editorial surfaces are elsewhere in this file set and every one of them carries a
/// policy.
/// </para>
/// </summary>
internal static class IntelligenceDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/countries/{countryCode}", async (
                string countryCode,
                string? locale,
                bool? recordView,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCountryIntelligenceQuery
                        {
                            CountryCode = countryCode,
                            Locale = locale,
                            RecordView = recordView ?? true
                        },
                        cancellationToken)))
            .WithName("GetCountryIntelligence")
            .WithSummary(
                "Everything official about one country: notices, alerts, missions, emergency " +
                "numbers, visa advisories and the holidays inside the dashboard's horizon.");

        group.MapGet("/search", async (
                string? q,
                string? countryCode,
                string? category,
                string? severity,
                string? language,
                bool? officialOnly,
                bool? activeOnly,
                string? sort,
                string? cursor,
                int? limit,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchOfficialNoticesQuery
                        {
                            Text = q,
                            CountryCode = countryCode,
                            CategoryKeys = category is null ? [] : [category],
                            Severities = severity is null ? [] : [severity],
                            Language = language,
                            OfficialOnly = officialOnly ?? false,
                            ActiveOnly = activeOnly ?? false,
                            Sort = sort,
                            Cursor = cursor,
                            Limit = limit,
                            Locale = locale
                        },
                        cancellationToken)))
            .RequireRateLimiting(RateLimitPolicyNames.Search)
            .WithName("SearchOfficialNotices")
            .WithSummary(
                "Search official notices. An unavailable engine answers 503, never an empty page.");

        group.MapGet("/capabilities", async (
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetIntelligenceCapabilitiesQuery { Locale = locale },
                        cancellationToken)))
            .WithName("GetIntelligenceCapabilities")
            .WithSummary(
                "The categories, emergency kinds, severities and sorts this deployment offers, " +
                "and which engine is answering search.");

        group.MapGet("/countries/{countryCode}/analytics", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetIntelligenceAnalyticsQuery { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ReadAnalytics)
            .WithName("GetIntelligenceAnalytics")
            .WithSummary(
                "Usage figures for one country. Totals only — there is no field for who.");
    }
}
