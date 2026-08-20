using Diaspora.Modules.Housing.Application.Catalog;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// The configurable catalogues.
/// <para>
/// Anonymous, because a client needs the accommodation-type picker and the amenity groups before it
/// can render a search form at all - and both are reference data rather than anybody content.
/// </para>
/// </summary>
internal static class HousingCatalogEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/accommodation-types", async (
                [FromQuery] bool? includeInactive,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListAccommodationTypesQuery
                        {
                            IncludeInactive = includeInactive ?? false
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListAccommodationTypes")
            .WithSummary("The accommodation-type catalogue.");

        group.MapGet("/amenities", async (
                [FromQuery] bool? includeInactive,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListHousingAmenitiesQuery
                        {
                            IncludeInactive = includeInactive ?? false
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListHousingAmenities")
            .WithSummary("The amenity catalogue, grouped as a listing form renders it.");

        MapManagement(group);
    }

    /// <summary>
    /// The write half. <b>This is what makes the catalogues configurable rather than merely
    /// extensible</b> — a contributor needs a deployment, and a deployment operating in a market
    /// where "self-contain" is what people actually rent needs to add it on a Tuesday.
    /// <para>
    /// <c>PUT</c> rather than <c>POST</c>, because the key decides create from update and an
    /// operator repeating a request should not get a second entry.
    /// </para>
    /// </summary>
    private static void MapManagement(RouteGroupBuilder group)
    {
        group.MapPut("/accommodation-types", async (
                UpsertAccommodationTypeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpsertAccommodationTypeCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ManageCatalogue)
            .WithName("UpsertAccommodationType")
            .WithSummary("Add or revise an accommodation type.");

        group.MapPut("/amenities", async (
                UpsertHousingAmenityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpsertHousingAmenityCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ManageCatalogue)
            .WithName("UpsertHousingAmenity")
            .WithSummary("Add or revise an amenity.");
    }
}
