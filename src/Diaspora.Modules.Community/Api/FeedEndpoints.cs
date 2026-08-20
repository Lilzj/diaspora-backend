using Diaspora.Modules.Community.Application.Feeds;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>
/// Feeds.
/// <para>
/// <b>One route for every feed the platform has</b>, because the feed key is data rather than a
/// path a developer chose. Adding a feed adds no route and no handler — which is what makes the
/// brief's "independently extensible feeds" true rather than aspirational.
/// </para>
/// <para>
/// The query parameters are dual-bound where the client spells them two ways — <c>radiusKm</c>
/// and <c>distanceRadius</c>, <c>cityName</c> and <c>city</c> — matching the convention B4 froze
/// for the same reason.
/// </para>
/// </summary>
internal static class FeedEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/feeds/{feedKey}", async (
                string feedKey,
                string? cursor,
                int? limit,
                string? ranking,
                string? countryCode,
                Guid? areaId,
                string? cityName,
                string? city,
                double? latitude,
                double? longitude,
                double? radiusKm,
                double? distanceRadius,
                string? category,
                string? urgency,
                string? hashtag,
                Guid? subjectId,
                bool? includeExpired,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetFeedQuery
                        {
                            FeedKey = feedKey,
                            Cursor = cursor,
                            Limit = limit ?? CommunityLimits.DefaultFeedPageSize,
                            RankingKey = ranking,
                            CountryCode = countryCode,
                            AreaId = areaId,
                            CityName = cityName ?? city,
                            Latitude = latitude,
                            Longitude = longitude,
                            RadiusKm = radiusKm ?? distanceRadius,
                            Category = category,
                            Urgency = urgency,
                            Hashtag = hashtag,
                            SubjectId = subjectId,
                            IncludeExpired = includeExpired ?? false
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("GetCommunityFeed")
            .WithSummary("A page of any registered feed. The key is data, not a route.");
    }
}
