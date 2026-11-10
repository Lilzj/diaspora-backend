using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Search;

/// <summary>
/// Turns coordinates into a place.
/// <para>
/// The server-side replacement for the frontend's direct call to <c>ipapi.co</c> during
/// registration, which sends every prospective member's IP address to an unvetted third party
/// with no agreement and no rate limit. Adoption is optional — the client is not broken by
/// ignoring this — but the reason to adopt it is not performance.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.Search)]
public sealed record ReverseGeocodeQuery : IQuery<LocationReferenceResponse>
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}
