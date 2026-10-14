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
        var scope = await _resolver.ResolveForDiscoveryAsync(
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

        // ── A guide is national content ─────────────────────────────────────────────────────
        //
        // Most guides have no pin and are about the whole country, so a point is answered by the
        // country it is in. Nothing infers a country from a coordinate any more, so it is the
        // request's, or — when the request named none — the signed-in member's saved one, from
        // the same fallback a request with no geography at all uses.
        if (scope.IsSuccess
            && scope.Value is { CountryCode: null, Bounds: not null } spatial
            && (spatial.IsAroundPoint || spatial.IsViewport)
            && viewerId is not null)
        {
            var saved = await _resolver.ResolveForDiscoveryAsync(
                new GeographicScopeRequest { UserId = viewerId }, cancellationToken);

            if (saved.IsSuccess && saved.Value.CountryCode is { } country)
            {
                return Result.Success(spatial with { CountryCode = country });
            }
        }

        return scope;
    }

    /// <summary>
    /// Turns a resolved scope into the geographic half of a guide query filter, without the place
    /// catalogue (14 Oct 2026).
    /// <list type="bullet">
    /// <item><b>A circle or a viewport</b>: guides pinned inside the rectangle, and every unpinned
    /// guide of the country the point is in (the request's, else the member's saved one) — a
    /// guide is national content, and most have no pin. Without a country, unpinned guides in
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
                UnpinnedCountryCode = scope.CountryCode?.Value,
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
