using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The one place B8 touches geography.
/// <para>
/// The client sends city, coordinates and radius in any combination, plus B4's radius
/// sentinels; all of it is handed to <see cref="IGeographicScopeResolver"/> and normalised
/// once. B8 parses no radius, resolves no city name and computes no distance — which is
/// precisely what B4's contract freeze promises verticals, and this class is B8 taking it up.
/// </para>
/// <para>
/// <b>14 Oct 2026: no catalogue.</b> A guide has a country, the town its author typed and, since
/// the same day, an optional pin. A circle is answered by the pin and by unpinned guides in the town
/// the request named; a town by its name in either letter case; a country by the country.
/// </para>
/// </summary>
public sealed class GuideGeographicScopeAdapter
{
    private readonly IGeographicScopeResolver _resolver;

    public GuideGeographicScopeAdapter(IGeographicScopeResolver resolver) => _resolver = resolver;

    /// <summary>Resolves whatever geography a nearby request carried.</summary>
    public async Task<Result<GeographicScope>> ResolveAsync(
        ListNearbyGuidesRequest request,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Without the catalogue (14 Oct 2026). The area id old app builds still send is ignored.
        return await _resolver.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                UserId = viewerId
            },
            cancellationToken);
    }

    /// <summary>
    /// Turns a resolved scope into the geographic half of a guide query filter, without the place
    /// catalogue (14 Oct 2026).
    /// <list type="bullet">
    /// <item><b>A circle</b>: guides pinned inside the rectangle around it, and unpinned guides in
    /// the town the request named.</item>
    /// <item><b>A town</b>: the country and the town's name, either letter case.</item>
    /// <item><b>A country</b>: the country.</item>
    /// </list>
    /// An unbounded scope narrows nothing, which is "everywhere" rather than "nowhere".
    /// </summary>
    public static GuideQueryFilter Apply(GuideQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded)
        {
            return filter;
        }

        var narrowed = filter with { CountryCode = scope.CountryCode?.Value ?? filter.CountryCode };

        if (scope.Bounds is { } box && (scope.IsAroundPoint || scope.IsViewport))
        {
            return narrowed with
            {
                CityName = null,
                UnpinnedCityName = scope.IsViewport ? null : scope.CityName,
                MinLatitude = box.South,
                MaxLatitude = box.North,
                MinLongitude = box.West,
                MaxLongitude = box.East
            };
        }

        return narrowed with
        {
            CityName = scope.Radius.Scope == RadiusScope.Country ? null : scope.CityName
        };
    }
}
