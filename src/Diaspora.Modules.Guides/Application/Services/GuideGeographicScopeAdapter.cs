using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Guides;
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
/// A guide is jurisdiction-scoped rather than point-scoped — it has a country and optionally
/// an area, never a coordinate — so applying a scope to a guide query narrows by country, and
/// finer narrowing (city, area path) happens where the rows are read. The scope's own
/// resolution is still B4's: "near me" means whatever B4 says it means, identically to every
/// other vertical.
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

        return await _resolver.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                CountryCode = request.CountryCode,
                AreaId = request.AreaId is { } areaId ? AreaId.From(areaId) : null,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                UserId = viewerId,

                // Lenient: a city name the catalogue does not know should narrow nothing
                // rather than fail the browse. A guides screen that errors because somebody
                // typed "Londn" is worse than one that shows the country's guides.
                StrictCityResolution = false
            },
            cancellationToken);
    }

    /// <summary>
    /// Turns a resolved scope into the geographic half of a guide query filter.
    /// <para>
    /// Country only, because that is what the filter can translate — the guide table indexes
    /// jurisdiction, not geometry. An unbounded scope narrows nothing, which is "everywhere"
    /// rather than "nowhere", per B4's own flag.
    /// </para>
    /// </summary>
    public static GuideQueryFilter Apply(GuideQueryFilter filter, GeographicScope scope)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.IsUnbounded || scope.CountryCode is null)
        {
            return filter;
        }

        return filter with { CountryCode = scope.CountryCode.Value };
    }
}
