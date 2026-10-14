using Diaspora.Modules.CommunityLife.Application.Discovery;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The anonymous surfaces: browse, calendar, and the neighbourhood view.
/// <para>
/// <b>All anonymous, deliberately.</b> Somebody who has just arrived in a city looking for a place
/// of worship or a support group has not signed up yet, and a platform that asked them to first
/// would be behind the need it exists to serve.
/// </para>
/// </summary>
internal static class CommunityLifeDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/events", async (
                [AsParameters] EventSearchParameters parameters,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(parameters.ToQuery(), cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchCommunityEvents")
            .WithSummary("The event browse screen.");

        group.MapGet("/calendar", async (
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] string? countryCode,
                [FromQuery] Guid? neighborhoodAreaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCommunityCalendarQuery
                        {
                            From = from,
                            To = to,
                            CountryCode = countryCode,
                            NeighborhoodAreaId = neighborhoodAreaId
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetCommunityCalendar")
            .WithSummary("Everything happening in a window, from every registered source.");

        // The place is B4's and its profile is B11's. This module answers only "what is happening
        // there", which is why the surface sits under this prefix rather than under theirs.
        group.MapGet("/neighborhoods/{areaId:guid}/events", async (
                Guid areaId,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchEventsQuery
                        {
                            NeighborhoodAreaId = areaId,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListNeighborhoodEvents")
            .WithSummary("What is happening in one neighbourhood.");

        // B12.1. The counts behind the list. Nothing new is stored — the analytics table has carried
        // per-neighbourhood totals since B12 and this is the route to them that was missing.
        group.MapGet("/neighborhoods/{areaId:guid}/activity", async (
                Guid areaId,
                [FromQuery] int? windowDays,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetNeighborhoodActivityQuery
                        {
                            AreaId = areaId,
                            WindowDays = windowDays
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("GetNeighborhoodActivity")
            .WithSummary("How much community life one neighbourhood has.");
    }
}

/// <summary>
/// The browse filters, bound from the query string.
/// <para>
/// A parameter object rather than eighteen arguments: the browse screen has genuinely that many
/// filters, and a handler signature that long is one nobody can add to safely.
/// </para>
/// </summary>
internal sealed record EventSearchParameters
{
    [FromQuery(Name = "q")]
    public string? SearchTerm { get; init; }

    [FromQuery(Name = "category")]
    public string[]? CategoryKeys { get; init; }

    [FromQuery(Name = "facet")]
    public string? Facet { get; init; }

    [FromQuery(Name = "organizationId")]
    public Guid? OrganizationId { get; init; }

    [FromQuery(Name = "countryCode")]
    public string? CountryCode { get; init; }

    [FromQuery(Name = "city")]
    public string? CityName { get; init; }

    [FromQuery(Name = "neighborhoodAreaId")]
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved by B4.</summary>
    [FromQuery(Name = "bounds")]
    public string? Bounds { get; init; }

    /// <summary>A point to search around, with <c>longitude</c> and <c>radiusKm</c> (14 Oct 2026).</summary>
    [FromQuery(Name = "latitude")]
    public double? Latitude { get; init; }

    [FromQuery(Name = "longitude")]
    public double? Longitude { get; init; }

    /// <summary>Kilometres, or Location's sentinels: <c>-1</c> this town, <c>-2</c> this country.</summary>
    [FromQuery(Name = "radiusKm")]
    public double? RadiusKm { get; init; }

    [FromQuery(Name = "startsAfter")]
    public DateTimeOffset? StartsAfter { get; init; }

    [FromQuery(Name = "startsBefore")]
    public DateTimeOffset? StartsBefore { get; init; }

    [FromQuery(Name = "free")]
    public bool? FreeOnly { get; init; }

    [FromQuery(Name = "familyFriendly")]
    public bool? FamilyFriendlyOnly { get; init; }

    [FromQuery(Name = "childFriendly")]
    public bool? ChildFriendlyOnly { get; init; }

    [FromQuery(Name = "students")]
    public bool? StudentOnly { get; init; }

    [FromQuery(Name = "featured")]
    public bool? FeaturedOnly { get; init; }

    [FromQuery(Name = "verifiedOrganizers")]
    public bool? VerifiedOrganizersOnly { get; init; }

    [FromQuery(Name = "hasPlaces")]
    public bool? HasPlacesRemaining { get; init; }

    [FromQuery(Name = "attendanceMode")]
    public string? AttendanceMode { get; init; }

    [FromQuery(Name = "includePast")]
    public bool? IncludePast { get; init; }

    [FromQuery(Name = "sort")]
    public string? SortOrder { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    public SearchEventsQuery ToQuery() =>
        new()
        {
            SearchTerm = SearchTerm,
            CategoryKeys = CategoryKeys,
            Facet = Facet,
            OrganizationId = OrganizationId,
            CountryCode = CountryCode,
            CityName = CityName,

            // Accepted from old app builds and ignored (14 Oct 2026): a catalogue node narrows no
            // member's browse. The neighbourhood pages keep their own route until Phase 2.
            NeighborhoodAreaId = null,
            Bounds = Bounds,
            Latitude = Latitude,
            Longitude = Longitude,
            RadiusKm = RadiusKm,
            StartsAfter = StartsAfter,
            StartsBefore = StartsBefore,
            FreeOnly = FreeOnly,
            FamilyFriendlyOnly = FamilyFriendlyOnly,
            ChildFriendlyOnly = ChildFriendlyOnly,
            StudentOnly = StudentOnly,
            FeaturedOnly = FeaturedOnly,
            VerifiedOrganizersOnly = VerifiedOrganizersOnly,
            HasPlacesRemaining = HasPlacesRemaining,
            AttendanceMode = AttendanceMode,
            IncludePast = IncludePast ?? false,
            SortOrder = SortOrder,
            Cursor = Cursor,
            Limit = Limit
        };
}
