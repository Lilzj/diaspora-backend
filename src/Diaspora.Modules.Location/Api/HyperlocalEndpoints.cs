using Diaspora.Modules.Location.Application.Nearby;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// The hyperlocal engine's routes.
/// <para>
/// <c>/nearby</c> is the one that matters. It replaces the four separate calls and the nested
/// fallback chain the frontend's detail screens make today, and it grows a new group whenever
/// a vertical registers a provider — without this file changing.
/// </para>
/// </summary>
internal static class HyperlocalEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/nearby", async (
                double? latitude,
                double? longitude,
                double? radiusKm,
                double? distanceRadius,
                Guid? areaId,
                string? cityName,
                string? city,
                string? countryCode,
                string? resourceTypes,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetNearbyQuery
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        RadiusKm = LocationRouteParsing.ResolveRadius(radiusKm, distanceRadius),
                        AreaId = areaId,
                        CityName = LocationRouteParsing.ResolveCity(cityName, city),
                        CountryCode = countryCode,
                        ResourceTypes = ParseResourceTypes(resourceTypes),
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("GetNearby")
            .WithSummary("Everything near a point, from every vertical that registered a provider.");

        group.MapGet("/within-bounds", async (
                double north,
                double south,
                double east,
                double west,
                string? levels,
                string? countryCode,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SearchWithinBoundsQuery
                    {
                        North = north,
                        South = south,
                        East = east,
                        West = west,
                        Levels = LocationRouteParsing.ParseLevels(levels),
                        CountryCode = countryCode,
                        Limit = limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("SearchWithinBounds")
            .WithSummary("The catalogue nodes inside a map viewport.");

        group.MapGet("/map/clusters", async (
                string bounds,
                string? level,
                string? resourceTypes,
                string? countryCode,
                double? cellSizeKm,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetMapClustersQuery
                    {
                        Bounds = bounds,
                        Level = level,
                        ResourceTypes = ParseResourceTypes(resourceTypes),
                        CountryCode = countryCode,
                        CellSizeKm = cellSizeKm
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("GetMapClusters")
            .WithSummary("Aggregated markers for a viewport, by square grid cell over each row's own coordinates.");

        group.MapGet("/map/boundaries", async (
                string bounds,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetMapBoundariesQuery
                    {
                        Bounds = bounds,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("GetMapBoundaries")
            .WithSummary("Retired 14 Oct 2026: always an empty page with a reason. Kept until Phase 3 removes it.");

        // POST rather than GET: a simplified city outline is several hundred coordinate pairs,
        // well past what proxies and browsers accept in a URL.
        group.MapPost("/within-polygon", async (
                PolygonSearchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var vertices = new List<GeoCoordinate>(request.Vertices.Count);

                foreach (var vertex in request.Vertices)
                {
                    var coordinate = GeoCoordinate.TryCreate(vertex.Latitude, vertex.Longitude);

                    if (coordinate.IsFailure)
                    {
                        return PlatformResults.Ok(
                            context,
                            SharedKernel.Results.Result.Failure<
                                IReadOnlyList<Contracts.V1.Responses.AreaSummaryResponse>>(
                                coordinate.Error));
                    }

                    vertices.Add(coordinate.Value);
                }

                var result = await sender.Send(
                    new SearchWithinPolygonQuery
                    {
                        Vertices = vertices,
                        Levels = ParseLevelList(request.Levels),
                        CountryCode = request.CountryCode,
                        Limit = request.Limit
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("SearchWithinPolygon")
            .WithSummary("The catalogue nodes inside a drawn outline.");

        group.MapPost("/distance", async (
                DistanceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CalculateDistanceQuery
                    {
                        FromLatitude = request.FromLatitude,
                        FromLongitude = request.FromLongitude,
                        ToLatitude = request.ToLatitude,
                        ToLongitude = request.ToLongitude
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Hyperlocal)
            .WithName("CalculateDistance")
            .WithSummary("How far apart two points are, with commute estimates.");

        group.MapGet("/capabilities", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetSpatialCapabilitiesQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("GetSpatialCapabilities")
            .WithSummary("What this deployment's geography can do, and who has contributed to it.");
    }

    private static string[] ParseResourceTypes(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IReadOnlyList<Domain.Enums.AdministrativeLevel> ParseLevelList(
        IReadOnlyList<string>? levels) =>
        levels is null || levels.Count == 0
            ? []
            : LocationRouteParsing.ParseLevels(string.Join(',', levels));
}
