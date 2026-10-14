using Diaspora.Modules.Location.Application.Places;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// Street-address search through the configured vendor.
/// <para>
/// <b>POST for reads, for the reason <c>POST /geo/routes</c> gives.</b> What a member types into an
/// address field is usually where they live, and a query string is written to every proxy and
/// access log on the way. A body is not.
/// </para>
/// </summary>
internal static class PlaceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/places/autocomplete", async (
                AutocompletePlacesRequest body,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new AutocompletePlacesQuery
                    {
                        Input = body.Input ?? string.Empty,
                        SessionToken = body.SessionToken ?? string.Empty,
                        NearLatitude = body.Near?.Latitude,
                        NearLongitude = body.Near?.Longitude,
                        NearRadiusMetres = body.NearRadiusMetres,
                        CountryCodes = body.CountryCodes ?? [],
                        Language = body.Language,
                        TownsOnly = body.TownsOnly == true,
                        RegionsOnly = body.RegionsOnly == true
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Search)
            .WithName("AutocompletePlaces")
            .WithSummary("Street-address and place suggestions for what a member has typed.");

        group.MapPost("/places/details", async (
                LookUpPlaceRequest body,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetPlaceDetailsQuery
                    {
                        PlaceId = body.PlaceId ?? string.Empty,
                        SessionToken = body.SessionToken ?? string.Empty,
                        Language = body.Language
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.Search)
            .WithName("GetPlaceDetails")
            .WithSummary("The coordinate and address of a picked suggestion. Ends the search session.");
    }
}

internal sealed record PlacePointRequest(double Latitude, double Longitude);

internal sealed record AutocompletePlacesRequest(
    string? Input,
    string? SessionToken,
    PlacePointRequest? Near,
    int? NearRadiusMetres,
    IReadOnlyList<string>? CountryCodes,
    string? Language,
    bool? TownsOnly = null,
    bool? RegionsOnly = null);

internal sealed record LookUpPlaceRequest(string? PlaceId, string? SessionToken, string? Language);
