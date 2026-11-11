using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Nearby;

/// <summary>
/// Everything near a point, from every vertical at once.
/// <para>
/// The endpoint that replaces four client-side calls and their fallback chain. Every
/// parameter is optional except that at least one of them has to narrow the request: with a
/// member id and nothing else it resolves to wherever they are, travel mode included.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.Hyperlocal)]
public sealed record GetNearbyQuery : IQuery<NearbyResponse>
{
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres, or the client's sentinels — <c>-1</c> city, <c>-2</c> country.</summary>
    public double? RadiusKm { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>Restricts the fan-out to named verticals. Empty asks all of them.</summary>
    public IReadOnlyList<string> ResourceTypes { get; init; } = [];

    /// <summary>The most items any single vertical may return.</summary>
    public int? Limit { get; init; }
}
