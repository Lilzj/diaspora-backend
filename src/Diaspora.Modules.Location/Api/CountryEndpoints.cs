using Diaspora.Modules.Location.Application.Areas;
using Diaspora.Modules.Location.Application.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// The country catalogue's routes.
/// <para>
/// These replace the 195-country array bundled into the mobile app, and the three
/// level-scoped sub-routes replace the hierarchy the app currently does not have at all.
/// </para>
/// </summary>
internal static class CountryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/countries", async (
                string? q,
                bool? supportedOnly,
                string? continent,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListCountriesQuery
                    {
                        Term = q,
                        SupportedOnly = supportedOnly,
                        Continent = continent,
                        Pagination = LocationRouteParsing.Paginate(page, pageSize)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("ListCountries")
            .WithSummary("Every country the platform lists, with metadata and support status.");

        group.MapGet("/countries/{code}", async (
                string code,
                bool? includeCounts,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetCountryQuery
                    {
                        CountryCode = code,
                        IncludeCounts = includeCounts ?? false
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName("GetCountry")
            .WithSummary("One country, optionally with the counts every vertical contributes.");

        // Three routes, one query. They differ only in the rung they ask for, and the
        // level-specific paths are what a client reads rather than ?level=City.
        MapLevel(group, "regions", AdministrativeLevel.Region, "ListCountryRegions",
            "The regions of a country.");

        MapLevel(group, "cities", AdministrativeLevel.City, "ListCountryCities",
            "The cities of a country.");

        MapLevel(group, "neighborhoods", AdministrativeLevel.Neighborhood,
            "ListCountryNeighborhoods", "Every neighbourhood in a country.");

        MapLevel(group, "states", AdministrativeLevel.StateProvince, "ListCountryStates",
            "The states or provinces of a country.");

        MapLevel(group, "districts", AdministrativeLevel.District, "ListCountryDistricts",
            "The districts of a country.");
    }

    private static void MapLevel(
        RouteGroupBuilder group,
        string segment,
        AdministrativeLevel level,
        string name,
        string summary)
    {
        group.MapGet($"/countries/{{code}}/{segment}", async (
                string code,
                string? q,
                int? page,
                int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new ListCountryAreasQuery
                    {
                        CountryCode = code,
                        Level = level,
                        Term = q,
                        Pagination = LocationRouteParsing.Paginate(page, pageSize)
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadCatalog)
            .WithName(name)
            .WithSummary(summary);
    }
}
