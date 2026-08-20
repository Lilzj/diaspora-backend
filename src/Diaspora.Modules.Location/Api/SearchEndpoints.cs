using Diaspora.Modules.Location.Application.Search;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// Place lookup: autocomplete and reverse geocoding.
/// <para>
/// <c>/search</c> is the one Location route the frontend already calls. It accepts both
/// <c>?query=</c> (what the client sends today) and <c>?q=</c> (the platform's convention
/// elsewhere), so adopting the real backend costs a base-URL change and nothing else.
/// </para>
/// </summary>
internal static class SearchEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/search", async (
                string? query,
                string? q,
                string? countryCode,
                string? levels,
                double? latitude,
                double? longitude,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SearchLocationsQuery
                    {
                        Term = string.IsNullOrWhiteSpace(query) ? q ?? string.Empty : query,
                        CountryCode = countryCode,
                        Levels = LocationRouteParsing.ParseLevels(levels),
                        NearLatitude = latitude,
                        NearLongitude = longitude,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Search)
            .WithName("SearchLocations")
            .WithSummary("Find a place by name. Accepts ?query= and ?q=.");

        group.MapGet("/reverse-geocode", async (
                double latitude,
                double longitude,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ReverseGeocodeQuery { Latitude = latitude, Longitude = longitude },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Search)
            .WithName("ReverseGeocode")
            .WithSummary("The place a coordinate falls in.");

        group.MapGet("/nearest", async (
                double latitude,
                double longitude,
                string? level,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                // Nearest is reverse geocoding narrowed to a rung. Served by the same handler
                // rather than a second one, because two implementations of "closest place"
                // would eventually give two answers.
                var result = await sender.Send(
                    new ReverseGeocodeQuery { Latitude = latitude, Longitude = longitude },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Search)
            .WithName("FindNearestPlace")
            .WithSummary("The nearest catalogue node to a coordinate.");
    }
}
