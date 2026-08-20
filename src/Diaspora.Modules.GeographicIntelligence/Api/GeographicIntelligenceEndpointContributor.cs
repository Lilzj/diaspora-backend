using Diaspora.Modules.GeographicIntelligence.Application.Capabilities;
using Diaspora.Modules.GeographicIntelligence.Application.Heat;
using Diaspora.Modules.GeographicIntelligence.Application.Intelligence;
using Diaspora.Modules.GeographicIntelligence.Application.Routing;
using Diaspora.Modules.GeographicIntelligence.Application.Scores;
using Diaspora.Modules.GeographicIntelligence.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.GeographicIntelligence.Api;

/// <summary>
/// The module's HTTP surface.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// authorisation, telemetry — and returns through <c>PlatformResults</c> so the envelope and
/// problem documents are byte-for-byte identical to every other module's. Nothing here is known to
/// the host.
/// </para>
/// <para>
/// <b>Route shapes come from the frontend, not from this module.</b> The client's
/// <c>maps-gap-api.ts</c> already declares every one of these — <c>/heatmap</c>,
/// <c>/neighborhoods/{id}/scores</c>, <c>/neighborhoods/compare?ids=</c>,
/// <c>/neighborhoods/{id}/cost-of-living</c>, <c>/neighborhoods/{id}/safety</c> — against its
/// development prefix, with the note that "when a backend phase ships heat-map aggregates, the
/// repository swaps this import for a real one and the request shapes already match". They match.
/// Only the prefix changes, from <c>/maps-gap/v0</c> to <c>/api/v1/geo</c>.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceEndpointContributor : IEndpointContributor
{
    public string Module => GeographicIntelligenceModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GeographicIntelligenceModuleMetadata.RoutePrefix)
            .WithTags("Geographic Intelligence");

        MapIntelligence(group);
        MapHeat(group);
        MapRouting(group);
        MapCapabilities(group);
    }

    private static void MapIntelligence(RouteGroupBuilder group)
    {
        // Ahead of `/{idOrSlug}/scores`, because "compare" would otherwise be resolved as a slug.
        // Routing matches literals before parameters in ASP.NET Core, so this is belt and braces —
        // and belt and braces is right for the one collision that would silently 404 a real screen.
        group.MapGet("/neighborhoods/compare", async (
                string ids,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new CompareNeighborhoodsQuery
                    {
                        IdsOrSlugs = ParseList(ids),
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.IntelligenceRead)
            .WithName("CompareNeighborhoodScores")
            .WithSummary("Score breakdowns for several neighbourhoods, composed identically.");

        group.MapGet("/neighborhoods/{idOrSlug}/scores", async (
                string idOrSlug,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetNeighborhoodScoreQuery
                    {
                        IdOrSlug = idOrSlug,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.IntelligenceRead)
            .WithName("GetNeighborhoodScores")
            .WithSummary("A neighbourhood's score dimensions, with provenance and stated gaps.");

        group.MapGet("/neighborhoods/{idOrSlug}/cost-of-living", async (
                string idOrSlug,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetCostOfLivingQuery
                    {
                        IdOrSlug = idOrSlug,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.IntelligenceRead)
            .WithName("GetNeighborhoodCostOfLiving")
            .WithSummary("The assessed cost band, and an explicit statement that no index exists.");

        group.MapGet("/neighborhoods/{idOrSlug}/safety", async (
                string idOrSlug,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetSafetyScoreQuery
                    {
                        IdOrSlug = idOrSlug,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.IntelligenceRead)
            .WithName("GetNeighborhoodSafety")
            .WithSummary("The safety surface. Always unavailable, with the reason and limitations.");
    }

    private static void MapHeat(RouteGroupBuilder group) =>
        group.MapGet("/heatmap", async (
                string layer,
                double north,
                double south,
                double east,
                double west,
                string? level,
                double? zoom,
                string? countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetHeatLayerQuery
                    {
                        Layer = layer,

                        // Recomposed into B4's `north,south,east,west` string so the antimeridian
                        // branch, the range checks and the ±180° handling are parsed once, by the
                        // module that owns them. A second parser here would be a second place for
                        // a wrapped viewport to be wrong.
                        Bounds = string.Join(
                            ',',
                            north.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            south.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            east.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            west.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Level = level,

                        // The client's `fetchHeatLayer` already sends this. Taking it is contract
                        // fidelity rather than a new parameter to negotiate.
                        Zoom = zoom,
                        CountryCode = countryCode
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.IntelligenceRead)
            .WithName("GetHeatLayer")
            .WithSummary("A density layer for a viewport, aggregated by place and suppressed.");

    /// <summary>
    /// The routing route.
    /// <para>
    /// <b>POST for a read, and the reason is privacy rather than length.</b> An origin is the most
    /// sensitive coordinate this platform accepts, and a query string is written to every proxy,
    /// gateway and access log between the device and the handler. A body is not.
    /// </para>
    /// </summary>
    private static void MapRouting(RouteGroupBuilder group) =>
        group.MapPost("/routes", async (
                RouteRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new PlanRouteQuery
                    {
                        OriginLatitude = request.Origin?.Latitude ?? double.NaN,
                        OriginLongitude = request.Origin?.Longitude ?? double.NaN,
                        DestinationLatitude = request.Destination?.Latitude ?? double.NaN,
                        DestinationLongitude = request.Destination?.Longitude ?? double.NaN,
                        Waypoints =
                        [
                            .. (request.Waypoints ?? [])
                                .Select(point => (point.Latitude, point.Longitude))
                        ],
                        TravelMode = request.TravelMode ?? string.Empty,
                        Alternatives = request.Alternatives,
                        DepartAt = request.DepartAt,
                        Language = request.Language
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.RoutingRead)
            .WithName("PlanRoute")
            .WithSummary("Plans a journey through the configured provider, or says there is none.");

    private static void MapCapabilities(RouteGroupBuilder group) =>
        group.MapGet("/capabilities", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetGeoCapabilitiesQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(GeographicIntelligencePolicies.CapabilitiesRead)
            .WithName("GetGeoCapabilities")
            .WithSummary("Which geographic capabilities this deployment actually has.");

    /// <summary>
    /// Splits the comma-separated <c>ids</c> the client already sends.
    /// <para>
    /// The client's <c>compareNeighborhoods</c> serialises with <c>ids.join(',')</c>, so that is
    /// what is parsed. Repeated <c>ids=</c> parameters would have been the other reasonable shape
    /// and it is not the one that shipped.
    /// </para>
    /// </summary>
    private static IReadOnlyList<string> ParseList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
